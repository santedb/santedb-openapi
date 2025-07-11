﻿/*
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
using RestSrvr;
using RestSrvr.Attributes;
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Http;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using SanteDB.Messaging.Metadata.Composer;
using SanteDB.Messaging.Metadata.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Messaging.Metadata.Rest
{
    /// <summary>
    /// Metadata Exchange
    /// </summary>
    /// <remarks>An implementation of a metadata exchange endpoint</remarks>
    [ServiceBehavior(Name = MetadataMessageHandler.ConfigurationName)]
    [ExcludeFromCodeCoverage]
    public class MetadataServiceBehavior : IMetadataServiceContract
    {
        /// <summary>
        /// Gets the trace source
        /// </summary>
        private readonly Tracer m_traceSource = new Tracer(MetadataConstants.TraceSourceName);

        /// <summary>
        /// Get the swagger documentation
        /// </summary>
        public object GetMetadata(String serviceName, String composer)
        {
            try
            {
                // Get the services and see if we have a document already
                return MetadataComposerUtil.GetComposer(composer).ComposeDocumentation(serviceName);
            }
            catch (Exception e)
            {
                this.m_traceSource.TraceEvent(EventLevel.Error, "Could not get documentation due to exception: {0}", e);
                throw new InvalidOperationException("Error constructing OpenAPI swagger.json file", e);
            }
        }

        /// <summary>
        /// Get the YAML documentation
        /// </summary>
        public Stream GetOpenApiDefinitions()
        {
            RestOperationContext.Current.OutgoingResponse.ContentType = "application/json";
            IEnumerable<ServiceEndpointOptions> services = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<MetadataConfigurationSection>().Services;
            if (services == null || services.Count() == 0)
            {
                services = ApplicationServiceContext.Current.GetService<IServiceManager>().GetServices().OfType<IApiEndpointProvider>().Select(o => new ServiceEndpointOptions(o));
            }

            var localPath = RestOperationContext.Current.IncomingRequest.Url.Segments[1];
            // Output YAML
            var sw = new StringBuilder();
            sw.Append("{ urls: [");
            var apis = services.ToArray().Where(o => o.Behavior != null);
            foreach (var api in apis)
            {
                var serviceName = typeof(ServiceEndpointType).GetField(api.ServiceType.ToString()).GetCustomAttribute<XmlEnumAttribute>()?.Name ?? api.ServiceType.ToString();
                sw.AppendFormat("{{ \"url\": \"{0}\", ", $"/{localPath}{serviceName}/swagger.json");
                sw.AppendFormat("\"name\": \"{0}\" }} ,", MetadataComposerUtil.GetElementDocumentation(api.Behavior.Type) ?? api.ServiceType.ToString());
            }
            sw.Remove(sw.Length - 2, 2);
            sw.Append("] }");
            return new MemoryStream(Encoding.UTF8.GetBytes(sw.ToString()));
        }

        /// <summary>
        /// Get the specified embedded resource
        /// </summary>
        public Stream Index(String content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    var requestUrl = RestOperationContext.Current.IncomingRequest.Url;
                    RestOperationContext.Current.OutgoingResponse.Redirect($"{requestUrl.Scheme}://{requestUrl.Host}:{requestUrl.Port}{requestUrl.AbsolutePath}/index.html");
                    return new MemoryStream();
                }

                string filename = content.Contains("?")
                    ? content.Substring(0, content.IndexOf("?", StringComparison.Ordinal))
                    : content;

                var contentPath = $"SanteDB.Messaging.Metadata.Docs.{filename.Replace("/", ".")}";

                if (!typeof(MetadataServiceBehavior).Assembly.GetManifestResourceNames().Contains(contentPath))
                {
                    RestOperationContext.Current.OutgoingResponse.StatusCode = 404;
                    return null;
                }
                else
                {
                    RestOperationContext.Current.OutgoingResponse.StatusCode = 200; // HttpStatusCode.OK;
                    //RestOperationContext.Current.OutgoingResponse.ContentLength = new FileInfo(contentPath).Length;
                    RestOperationContext.Current.OutgoingResponse.ContentType = DefaultContentTypeMapper.GetContentType(contentPath);
                    return typeof(MetadataServiceBehavior).Assembly.GetManifestResourceStream(contentPath);
                }
            }
            catch (Exception e)
            {
                RestOperationContext.Current.OutgoingResponse.StatusCode = 500;

                this.m_traceSource.TraceEvent(EventLevel.Error, e.ToString());
                return null;
            }
        }
    }
}