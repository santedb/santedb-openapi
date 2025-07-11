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
using Newtonsoft.Json;
using RestSrvr.Attributes;
using SanteDB.Core.Interop.Description;
using SanteDB.Messaging.Metadata.Composer;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{

    /// <summary>
    /// Represents the swagger parameter location
    /// </summary>
    public enum SwaggerParameterLocation
    {
        /// <summary>
        /// Location is in the body
        /// </summary>
        body,
        /// <summary>
        /// Location is in the path
        /// </summary>
        path,
        /// <summary>
        /// Location is in the query
        /// </summary>
        query
    }
    /// <summary>
    /// Represents the swagger parameter
    /// </summary>
    [JsonObject(nameof(SwaggerParameter))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerParameter : SwaggerSchemaElement
    {


        /// <summary>
        /// Constructor for serializer
        /// </summary>
        public SwaggerParameter()
        {

        }
        /// <summary>
        /// Create a swagger query parameter
        /// </summary>
        public SwaggerParameter(PropertyInfo queryFilter)
        {

            this.Name = queryFilter.GetSerializationName() ?? queryFilter.GetCustomAttribute<Core.Model.Attributes.QueryParameterAttribute>()?.ParameterName;
            this.Description = MetadataComposerUtil.GetElementDocumentation(queryFilter);
            this.Location = SwaggerParameterLocation.query;

            SwaggerSchemaElementType type = SwaggerSchemaElementType.@string;
            if (queryFilter.PropertyType.StripNullable().IsEnum)
            {
                this.Enum = queryFilter.PropertyType.StripNullable().GetFields().Select(f => f.GetCustomAttributes<XmlEnumAttribute>().FirstOrDefault()?.Name).Where(o => !string.IsNullOrEmpty(o)).ToList();
                this.Type = SwaggerSchemaElementType.@string;
            }
            else if (!m_typeMap.TryGetValue(queryFilter.PropertyType, out type))
            {
                this.Type = SwaggerSchemaElementType.@string;
            }
            else
            {
                this.Type = type;
            }
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public SwaggerParameter(SwaggerParameter copy) : base(copy)
        {
            this.Name = copy.Name;
            this.Location = copy.Location;
        }

        /// <summary>
        /// Generate parameter from metadata
        /// </summary>
        public SwaggerParameter(OperationParameterDescription description)
        {
            this.Name = description.Name;
            this.Location = description.Location == OperationParameterLocation.Body ? SwaggerParameterLocation.body :
                description.Location == OperationParameterLocation.Path ? SwaggerParameterLocation.path :
                SwaggerParameterLocation.query;

            if (description.Type != typeof(ResourceDescription))
            {
                this.Type = m_typeMap[description.Type];
            }
            else
            {
                this.Type = SwaggerSchemaElementType.@object;
                this.Schema = new SwaggerSchemaDefinition(description.ResourceType);
            }
        }

        /// <summary>
        /// Creates a new swagger parameter
        /// </summary>
        public SwaggerParameter(MethodInfo method, ParameterInfo parameter, RestInvokeAttribute operation)
        {
            this.Name = parameter.Name;
            this.Location = operation.UriTemplate.Contains($"{{{parameter.Name}}}") ? SwaggerParameterLocation.path : SwaggerParameterLocation.body;
            this.Description = MetadataComposerUtil.GetElementDocumentation(method, parameter);

            SwaggerSchemaElementType type = SwaggerSchemaElementType.@string;
            if (parameter.ParameterType.StripNullable().IsEnum)
            {
                this.Enum = parameter.ParameterType.StripNullable().GetFields().Select(f => f.GetCustomAttributes<XmlEnumAttribute>().FirstOrDefault()?.Name).Where(o => !string.IsNullOrEmpty(o)).ToList();
                this.Type = SwaggerSchemaElementType.@string;
            }
            else if (!m_typeMap.TryGetValue(parameter.ParameterType, out type))
            {
                this.Schema = new SwaggerSchemaDefinition()
                {
                    Reference = $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(parameter.ParameterType)}",
                    NetType = parameter.ParameterType
                };
            }
            else
            {
                this.Type = type;
            }
        }

        /// <summary>
        /// Gets or sets the name of the parameter
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets location of the parameter
        /// </summary>
        [JsonProperty("in")]
        public SwaggerParameterLocation Location { get; set; }


    }
}