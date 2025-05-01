/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2023-6-21
 */
using Newtonsoft.Json;
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interop;
using SanteDB.Core.Model.Attributes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.Messaging.Metadata.Composer;
using SanteDB.Rest.Common;
using SanteDB.Rest.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{
    /// <summary>
    /// Represents the root swagger document
    /// </summary>
    [JsonObject(nameof(SwaggerDocument))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerDocument
    {
        Tracer _Tracer;

        /// <summary>
        /// Gets the version of the swagger document
        /// </summary>
        public SwaggerDocument()
        {
            _Tracer = new Tracer(nameof(SwaggerDocument));
            this.Version = "2.0";
            this.Tags = new List<SwaggerTag>();
            this.Definitions = new Dictionary<String, SwaggerSchemaDefinition>();
            this.SecurityDefinitions = new Dictionary<String, SwaggerSecurityDefinition>();
            this.Paths = new Dictionary<string, SwaggerPath>();
            this.Schemes = new List<string>();
            this.Produces = new List<string>();
            this.Consumes = new List<string>();
        }

        /// <summary>
        /// Create new swagger document
        /// </summary>
        public SwaggerDocument(ServiceEndpointOptions service) : this()
        {
            var listen = new Uri(service.BaseUrl.First());
            this.BasePath = listen.AbsolutePath;
            listen = new Uri(ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<RestConfigurationSection>().ExternalHostPort ?? listen.ToString());
            this.Info = new SwaggerServiceInfo()
            {
                Title = MetadataComposerUtil.GetElementDocumentation(service.Behavior.Type, MetaDataElementType.Summary) ?? service.Behavior.Type.GetCustomAttribute<ServiceBehaviorAttribute>()?.Name,
                Description = MetadataComposerUtil.GetElementDocumentation(service.Behavior.Type, MetaDataElementType.Remarks),
                Version = $"{service.Behavior.Type.Assembly.GetName().Version.ToString()} ({service.Behavior.Type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion})"
            };
            this.Host = $"{listen.Host}:{listen.Port}".Replace("0.0.0.0", RestOperationContext.Current.IncomingRequest.Url.Host);

            // Get the schemes
            this.Schemes = new List<string>()
            {
                listen.Scheme
            };

            // What this option produces
            this.Produces.AddRange(service.Contracts.SelectMany(c => c.Type.GetCustomAttributes<ServiceProducesAttribute>().Select(o => o.MimeType)));
            this.Consumes.AddRange(service.Contracts.SelectMany(c => c.Type.GetCustomAttributes<ServiceConsumesAttribute>().Select(o => o.MimeType)));

            // Security requires us to peek into the runtime environment ...
            var serviceCaps = MetadataComposerUtil.GetServiceCapabilities(service.ServiceType);
            if (serviceCaps.HasFlag(ServiceEndpointCapabilities.BearerAuth))
            {
                using (AuthenticationContext.EnterSystemContext())
                {
                    var tokenUrl = new Uri(MetadataComposerUtil.ResolveService("acs").BaseUrl.FirstOrDefault());
                    if (tokenUrl.Host == "0.0.0.0" || tokenUrl.Host == "127.0.0.1") // Host is vanialla
                    {
                        tokenUrl = new Uri($"{listen.Scheme}://{listen.Host}:{listen.Port}{tokenUrl.AbsolutePath}");
                    }

                    this.SecurityDefinitions = new Dictionary<string, SwaggerSecurityDefinition>()
                    {
                        {  "svc_auth", new SwaggerSecurityDefinition()
                            {
                                Flow = SwaggerSecurityFlow.password,
                                Scopes = ApplicationServiceContext.Current.GetService<IRepositoryService<SecurityPolicy>>()?.Find(o=>o.ObsoletionTime == null).ToDictionary(o=>o.Oid, o=>o.Name),
                                TokenUrl = $"{tokenUrl.ToString().Replace("0.0.0.0", RestOperationContext.Current.IncomingRequest.Url.Host)}/oauth2_token",
                                Type = SwaggerSecurityType.oauth2
                            }
                        }
                    };
                }
            }
            else if(serviceCaps.HasFlag(ServiceEndpointCapabilities.BasicAuth))
            {
                this.SecurityDefinitions = new Dictionary<string, SwaggerSecurityDefinition>()
                {
                    {
                        "svc_auth", new SwaggerSecurityDefinition()
                        {
                            Type = SwaggerSecurityType.basic
                        }
                    }
                };
            }

            // Provides its own behavior configuration
            if (typeof(IServiceBehaviorMetadataProvider).IsAssignableFrom(service.Behavior.Type))
            {
                var smp = Activator.CreateInstance(service.Behavior.Type) as IServiceBehaviorMetadataProvider;
                this.InitializeViaMetadata(smp);
            }
            else
            {
                this.InitializeViaReflection(service);
            }

            // Now we want to add a definition for all references
            // This LINQ expression allows for scanning of any properties where there is currently no definition for a particular type
            var missingDefns = this.Paths.AsParallel().SelectMany(
                    o => o.Value.SelectMany(p => p.Value?.Parameters.Select(r => r.Schema))
                        .Union(o.Value.SelectMany(p => p.Value?.Responses?.Values?.Select(r => r.Schema))))
                .Union(this.Definitions.AsParallel().Where(o => o.Value.Properties != null).SelectMany(o => o.Value.Properties.Select(p => p.Value.Items ?? p.Value.Schema)))
                .Union(this.Definitions.AsParallel().Where(o => o.Value.AllOf != null).SelectMany(o => o.Value.AllOf))
                .Select(s => s?.NetType)
                .Where(o => o != null && !this.Definitions.ContainsKey(MetadataComposerUtil.CreateSchemaReference(o)))
                .Distinct();
            while (missingDefns.Count() > 0)
            {
                foreach (var def in missingDefns.AsParallel().ToList())
                {
                    var name = MetadataComposerUtil.CreateSchemaReference(def);
                    if (!this.Definitions.ContainsKey(name))
                    {
                        this.Definitions.Add(name, new SwaggerSchemaDefinition(def));
                    }
                }
            }

            // Create the definitions
            this.Tags = this.Tags.OrderBy(o => o.Name).ToList();
        }

        /// <summary>
        /// Initialize data via a metadata provider
        /// </summary>
        private void InitializeViaMetadata(IServiceBehaviorMetadataProvider smp)
        {
            this.Paths = smp.Description.Operations.GroupBy(o => o.Path).ToDictionary(o => o.Key, o => new SwaggerPath(o));
            this.Tags = this.Paths.SelectMany(o => o.Value.Values).SelectMany(o => o.Tags).Distinct().Select(o => new SwaggerTag(o, null)).ToList();
        }

        /// <summary>
        /// Initialize this class via reflection
        /// </summary>
        private void InitializeViaReflection(ServiceEndpointOptions service)
        {
            // Is there an options() method that can be called?
            var resourceHandlerTool = new ResourceHandlerTool(service.Contracts.First().Type);
            List<KeyValuePair<String, Type>> resourceTypes = null;
            var optionsMethod = service.Behavior.Type.GetRuntimeMethod("Options", Type.EmptyTypes);
            ServiceOptions serviceOptions = null;
            if (optionsMethod != null)
            {
                var behaviorInstance = Activator.CreateInstance(service.Behavior.Type);
                serviceOptions = optionsMethod.Invoke(behaviorInstance, null) as ServiceOptions;
                if (serviceOptions != null) // Remove unused resources
                {
                    resourceTypes = serviceOptions.Resources.Select(o => new KeyValuePair<string, Type>(o.ResourceName, o.ResourceType)).ToList();
                }
            }
            else
            {
                // Resource types
                resourceTypes = service.Contracts.SelectMany(c => c.Type.GetCustomAttributes<ServiceKnownResourceAttribute>().Select(o => new KeyValuePair<String, Type>(o.Type.GetCustomAttribute<XmlRootAttribute>()?.ElementName, o.Type)).Where(o => !String.IsNullOrEmpty(o.Key))).ToList();
            }

            // Construct the paths
            var operations = service.Contracts.SelectMany(c => c.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(m => m.GetCustomAttributes<RestInvokeAttribute>().Select(attr => new { Method = m, Attribute = attr }))
                    .Select(o => new { Rest = o.Attribute, ContractMethod = o.Method, BehaviorMethod = service.Behavior.Type.GetMethod(o.Method.Name, o.Method.GetParameters().Select(p => p.ParameterType).ToArray()) }))
                    .Where(o => o.Rest != null);

            // Create tags
            if (operations.Any(o => o.Rest.UriTemplate.Contains("{resourceType}")))
            {
                this.Tags = resourceTypes.Select(o => new SwaggerTag(o.Key, MetadataComposerUtil.GetElementDocumentation(o.Value, MetaDataElementType.Summary))).ToList();
            }

            // Process operations
            foreach (var operation in operations.GroupBy(o => o.Rest.UriTemplate.StartsWith("/") ? o.Rest.UriTemplate : "/" + o.Rest.UriTemplate))
            {
                // If the path does not contain {resourceType} and there are no ServiceKnownTypes then proceed
                var path = new SwaggerPath();
                foreach (var val in operation)
                {
                    // Process operations
                    var pathDefinition = new SwaggerPathDefinition(val.BehaviorMethod, val.ContractMethod, val.Rest);
                    path.Add(val.Rest.Method.ToLower(), pathDefinition);
                    if (pathDefinition.Consumes.Count == 0)
                    {
                        pathDefinition.Consumes.AddRange(this.Consumes);
                    }

                    if (pathDefinition.Produces.Count == 0)
                    {
                        pathDefinition.Produces.AddRange(this.Produces);
                    }

                    // Any faults?
                    foreach (var flt in service.Contracts.SelectMany(t => t.Type.GetCustomAttributes<ServiceFaultAttribute>()))
                    {
                        pathDefinition.Responses.Add(flt.StatusCode, new SwaggerSchemaElement()
                        {
                            Description = flt.Condition,
                            Schema = new SwaggerSchemaDefinition()
                            {
                                Reference = $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(flt.FaultType)}",
                                NetType = flt.FaultType
                            }
                        });
                    }
                }

                if (operation.Key.Contains("{resourceType}"))
                {
                    foreach (var resource in resourceTypes)
                    {

                        var resourcePath = operation.Key.Replace("{resourceType}", resource.Key);
                        if (this.Paths.ContainsKey(resourcePath) ||
                            resourcePath.Contains("history") && !typeof(IVersionedData).IsAssignableFrom(resource.Value))
                        {
                            continue;
                        }

                        // Create a copy of the path and customize it to the resource
                        List<String> unsupportedVerbs = new List<string>();

                        // Get the resource options for this resource
                        ServiceResourceOptions resourceOptions = serviceOptions?.Resources.FirstOrDefault(o => o.ResourceName == resource.Key);

                        var subPath = new SwaggerPath(path);
                        foreach (var v in subPath)
                        {
                            // Check that this resource is supported
                            var resourceCaps = resourceOptions?.Capabilities.FirstOrDefault(c => c.Capability == MetadataComposerUtil.VerbToCapability(v.Key, v.Value.Parameters.Count));
                            if (resourceOptions != null && resourceCaps == null &&
                                v.Key != "head" && v.Key != "options" && v.Key != "search")
                            {
                                unsupportedVerbs.Add(v.Key);
                                continue;
                            }

                            // Add tags for resource
                            v.Value.Tags.Add(resource.Key);
                            v.Value.Parameters.RemoveAll(o => o.Name == "resourceType");

                            // Security?
                            if (this.SecurityDefinitions.Count > 0 && resourceCaps?.Demand.Length > 0)
                            {
                                v.Value.Security = new List<SwaggerPathSecurity>()
                                {
                                    new SwaggerPathSecurity()
                                    {
                                        { "svc_auth", resourceCaps.Demand.Distinct().ToList() }
                                    }
                                };
                            }

                            // Query parameters?
                            if ((v.Key == "get" || v.Key == "head"))
                            {
                                // Build query parameters
                                v.Value.Parameters.AddRange(resource.Value.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                    .Where(o => o.GetCustomAttributes<XmlElementAttribute>().Any() || o.GetCustomAttribute<QueryParameterAttribute>() != null)
                                    .Select(o => new SwaggerParameter(o))
                                  );

                                // Get the resource handler type and its own search parameters
                                var resourceHandlerType = resourceHandlerTool.GetResourceHandler(resourceOptions.ResourceType).FirstOrDefault();

                                if (null == resourceHandlerType)
                                {
                                    _Tracer.TraceInfo($"GetResourceHandler for resource type \"{resourceOptions.ResourceType}\" is null. This is typically due to a missing service declaration in the configuration.");

                                    continue;
                                }

                                var customUrlParameters = resourceHandlerType.GetType().GetMethod(nameof(IApiResourceHandler.Query)).GetCustomAttributes<UrlParameterAttribute>();
                                if (customUrlParameters.Any())
                                {
                                    v.Value.Parameters.AddRange(customUrlParameters.Select(o => new SwaggerParameter()
                                    {
                                        Description = o.Description,
                                        Name = o.Name,
                                        Location = SwaggerParameterLocation.query,
                                        Type = SwaggerSchemaElement.m_typeMap[o.Type.StripNullable()],
                                        Format = SwaggerSchemaElement.m_formatMap[o.Type.StripNullable()],
                                        Required = o.Required
                                    }));
                                }

                                //v.Value.Parameters.AddRange(new SwaggerParameter[]
                                //{
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_offset",
                                //        Type = SwaggerSchemaElementType.number,
                                //        Description = "Offset of query results",
                                //        Location = SwaggerParameterLocation.query
                                //    },
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_count",
                                //        Type = SwaggerSchemaElementType.number,
                                //        Description = "Count of query results to include in result set",
                                //        Location = SwaggerParameterLocation.query
                                //    },
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_lean",
                                //        Type = SwaggerSchemaElementType.boolean,
                                //        Description = "When true, the server will only return minimal data by removing duplicates from the bundle's resource[] property. NOTE: This means that the .count parameter may not match the number of items in resource[], however you should continue to use the .count parameter to increase your offset",
                                //        Location = SwaggerParameterLocation.query
                                //    },
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_orderBy",
                                //        Type = SwaggerSchemaElementType.@string,
                                //        Description = "Indicates a series of parameters to order the result set by",
                                //        Location = SwaggerParameterLocation.query
                                //    },
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_queryId",
                                //        Type = SwaggerSchemaElementType.@string,
                                //        Description = "The unique identifier for the query (for continuation)",
                                //        Location = SwaggerParameterLocation.query
                                //    },
                                //    new SwaggerParameter()
                                //    {
                                //        Name = "_viewModel",
                                //        Type = SwaggerSchemaElementType.@string,
                                //        Description = "When using the view-model content-type, the view model to use",
                                //        Location = SwaggerParameterLocation.query,
                                //        Enum = new List<string>()
                                //        {
                                //            "min",
                                //            "max"
                                //        }
                                //    }
                                //});
                            }

                            // Replace the response if necessary
                            var resourceSchemaRef = new SwaggerSchemaDefinition()
                            {
                                NetType = resource.Value,
                                Reference = $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(resource.Value)}"
                            };
                            SwaggerSchemaElement schema = null;
                            if (v.Value.Responses.TryGetValue(200, out schema))
                            {
                                schema.Schema = resourceSchemaRef;
                            }

                            if (v.Value.Responses.TryGetValue(201, out schema))
                            {
                                schema.Schema = resourceSchemaRef;
                            }

                            // Replace the body if necessary
                            var bodyParm = v.Value.Parameters.FirstOrDefault(o => o.Location == SwaggerParameterLocation.body && o.Schema?.NetType?.IsAssignableFrom(resource.Value) == true);
                            if (bodyParm != null)
                            {
                                bodyParm.Schema = resourceSchemaRef;
                            }
                        } // foreach subpath

                        // Add the resource path?
                        foreach (var nv in unsupportedVerbs)
                        {
                            subPath.Remove(nv);
                        }

#if DEBUG
                        //Check for a condition that previously caused an exception.
                        if ((resourcePath.Contains("{childResourceType}") || resourcePath.Contains("{operationName}")) && null == resourceOptions?.ChildResources)
                        {
                            _Tracer.TraceUntestedWarning();
                            _Tracer.TraceWarning("Resource path contains child resource type but resourceOptions.ChildResources is null. This would previously have produced an exception and may indicate a configuration issue. This message will not appear in a release build.");
                        }

#endif


                        // Child resources? we want to multiply them if so
                        if ((resourcePath.Contains("{childResourceType}") || resourcePath.Contains("{operationName}")) && resourceOptions?.ChildResources?.Count > 0)
                        {
                            foreach (var sp in subPath)
                            {
                                sp.Value.Parameters.RemoveAll(o => o.Name == "childResourceType" || o.Name == "operationName");
                            }

                            // Correct for child resources - rewriting the schema and opts
                            foreach (var cp in resourceOptions.ChildResources)
                            {

                                // Bound to
                                if (resourcePath.Contains("{id}") && !cp.Scope.HasFlag(ChildObjectScopeBinding.Instance) ||
                                    !resourcePath.Contains("{id}") && !cp.Scope.HasFlag(ChildObjectScopeBinding.Class) ||
                                    resourcePath.Contains("$") && !cp.Classification.HasFlag(ChildObjectClassification.RpcOperation) ||
                                    !resourcePath.Contains("$") && !cp.Classification.HasFlag(ChildObjectClassification.Resource))
                                {
                                    continue;
                                }

                                var newPath = new SwaggerPath();
                                foreach (var v in subPath)
                                {
                                    var childCaps = cp.Capabilities.FirstOrDefault(c => c.Capability == MetadataComposerUtil.VerbToCapability(v.Key, v.Value.Parameters.Count));
                                    if (childCaps != null)
                                    {
                                        newPath.Add(v.Key, new SwaggerPathDefinition(v.Value));
                                    }
                                }

                                this.Paths.Add(resourcePath.Replace("{childResourceType}", cp.ResourceName).Replace("{operationName}", cp.ResourceName), newPath);
                                foreach (var sp in newPath)
                                {

                                    // Replace the response if necessary
                                    var resourceSchemaRef = new SwaggerSchemaDefinition()
                                    {
                                        NetType = cp.ResourceType,
                                        Reference = $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(cp.ResourceType)}"
                                    };
                                    SwaggerSchemaElement schema = null;
                                    if (sp.Value.Responses.TryGetValue(200, out schema))
                                    {
                                        schema.Schema = resourceSchemaRef;
                                    }
                                    if (sp.Value.Responses.TryGetValue(201, out schema))
                                    {
                                        schema.Schema = resourceSchemaRef;
                                    }

                                    // Replace the body if necessary
                                    var bodyParm = sp.Value.Parameters.FirstOrDefault(o => o.Location == SwaggerParameterLocation.body && o.Schema?.NetType?.IsAssignableFrom(resource.Value) == true);
                                    if (bodyParm != null)
                                    {
                                        bodyParm.Schema = resourceSchemaRef;
                                    }
                                }
                            }
                        }
                        else if (!resourcePath.Contains("{childResourceType}"))
                        {
                            this.Paths.Add(resourcePath, subPath);
                        }
                    }
                }
                else
                {
                    this.Paths.Add(operation.Key, path);
                }
            }
        }

        /// <summary>
        /// Gets or sets the service info
        /// </summary>
        [JsonProperty("info")]
        public SwaggerServiceInfo Info { get; set; }

        /// <summary>
        /// Gets or sets the base-path
        /// </summary>
        [JsonProperty("basePath")]
        public String BasePath { get; set; }

        /// <summary>
        /// Gets the host of this swagger
        /// </summary>
        [JsonProperty("host")]
        public String Host { get; set; }

        /// <summary>
        /// Gets the schemes
        /// </summary>
        [JsonProperty("schemes")]
        public List<String> Schemes { get; set; }

        /// <summary>
        /// Gets or sets the version
        /// </summary>
        [JsonProperty("swagger")]
        public String Version { get; set; }

        /// <summary>
        /// Gets or sets the paths
        /// </summary>
        [JsonProperty("paths")]
        public Dictionary<String, SwaggerPath> Paths { get; set; }

        /// <summary>
        /// Gets or sets the definitions
        /// </summary>
        [JsonProperty("definitions")]
        public Dictionary<String, SwaggerSchemaDefinition> Definitions { get; set; }

        /// <summary>
        /// Gets or sets the security definitions
        /// </summary>
        [JsonProperty("securityDefinitions")]
        public Dictionary<String, SwaggerSecurityDefinition> SecurityDefinitions { get; set; }

        /// <summary>
        /// Gets or sets the tags
        /// </summary>
        [JsonProperty("tags")]
        public List<SwaggerTag> Tags { get; set; }

        /// <summary>
        /// Gets the list of types this service produces
        /// </summary>
        [JsonProperty("produces")]
        public List<String> Produces { get; set; }

        /// <summary>
        /// Gets the list of types this ervice consumes
        /// </summary>
        [JsonProperty("consumes")]
        public List<String> Consumes { get; set; }
    }
}