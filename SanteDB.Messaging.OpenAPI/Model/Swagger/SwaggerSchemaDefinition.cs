/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using Newtonsoft.Json;
using SanteDB.Core.Interop.Description;
using SanteDB.Messaging.Metadata.Composer;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{
    /// <summary>
    /// Represents a swagger schema definition
    /// </summary>
    [JsonObject(nameof(SwaggerSchemaDefinition))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerSchemaDefinition
    {

        /// <summary>
        /// Default ctor
        /// </summary>
        public SwaggerSchemaDefinition()
        {
            //this.Properties = new Dictionary<string, SwaggerSchemaElement>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public SwaggerSchemaDefinition(SwaggerSchemaDefinition copy) : this()
        {
            this.Type = copy.Type;
            this.Format = copy.Format;
            this.Reference = copy.Reference;
            this.Description = copy.Description;
            if (copy.Properties != null)
            {
                this.Properties = copy.Properties.ToDictionary(o => o.Key, o => new SwaggerSchemaElement(o.Value));
            }

            if (copy.AllOf != null)
            {
                this.AllOf = copy.AllOf.Select(o => new SwaggerSchemaDefinition(o)).ToList();
            }

            this.NetType = copy.NetType;
        }

        /// <summary>
        /// Create a schema definition based on a type
        /// </summary>
        public SwaggerSchemaDefinition(Type schemaType)
        {
            this.Description = MetadataComposerUtil.GetElementDocumentation(schemaType, MetaDataElementType.Summary);
            this.NetType = schemaType;
            this.Type = SwaggerSchemaElementType.@object;

            this.Properties = schemaType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttributes<XmlElementAttribute>()?.Any() == true || p.GetCustomAttribute<JsonPropertyAttribute>() != null)
                .Select(p2 => new {Name = p2.GetSerializationName(), Value = p2})
                .Where(p3 => p3.Name != null)
                .ToDictionary(o => o.Name, o => new SwaggerSchemaElement(o.Value));

            // XML info
            var xmlType = schemaType.GetCustomAttribute<XmlTypeAttribute>();
            if (xmlType != null)
            {
                this.Xml = new SwaggerXmlInfo(xmlType);
            }
        }

        /// <summary>
        /// Create a resource schema definition from metadata
        /// </summary>
        public SwaggerSchemaDefinition(ResourceDescription resourceType)
        {

            this.Properties = resourceType.Properties?.ToDictionary(o => o.Name, o => new SwaggerSchemaElement(o));
            this.Description = resourceType.Description;

        }

        /// <summary>
        /// Gets or set a reference to another object
        /// </summary>
        [JsonProperty("$ref")]
        public string Reference { get; set; }

        /// <summary>
        /// Gets or sets the xml information
        /// </summary>
        [JsonProperty("xml")]
        public SwaggerXmlInfo Xml { get; set; }

        /// <summary>
        /// Represents a property
        /// </summary>
        [JsonProperty("properties")]
        public Dictionary<String, SwaggerSchemaElement> Properties { get; set; }

        /// <summary>
        /// Represents an all-of relationship
        /// </summary>
        [JsonProperty("allOf")]
        public List<SwaggerSchemaDefinition> AllOf { get; set; }

        /// <summary>
        /// Gets the .net datatype
        /// </summary>
        [JsonIgnore]
        public Type NetType { get; set; }

        /// <summary>
        /// Gets the description of the datatype
        /// </summary>
        [JsonProperty("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets the type
        /// </summary>
        [JsonProperty("type")]
        public SwaggerSchemaElementType? Type { get; internal set; }

        /// <summary>
        /// Gets the type
        /// </summary>
        [JsonProperty("format")]
        public SwaggerSchemaElementFormat? Format { get; internal set; }
    }
}