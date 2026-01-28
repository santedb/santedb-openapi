/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Interop.Description;
using SanteDB.Messaging.Metadata.Composer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{

    /// <summary>
    /// Schema element format
    /// </summary>
    public enum SwaggerSchemaElementFormat
    {
        /// <summary>
        /// Type UUID
        /// </summary>
        uuid,
        /// <summary>
        /// Tyoe DATE
        /// </summary>
        date,
        /// <summary>
        /// Type Float
        /// </summary>
        @float,
        /// <summary>
        /// Type double
        /// </summary>
        @double,
        /// <summary>
        /// Type int32
        /// </summary>
        @int32,
        /// <summary>
        /// Type int64
        /// </summary>
        @int64,
        /// <summary>
        /// Type byte
        /// </summary>
        @byte
    }

    /// <summary>
    /// Represents the swagger parameter type
    /// </summary>
    public enum SwaggerSchemaElementType
    {
        /// <summary>
        /// Parameter is a string
        /// </summary>
        @string,
        /// <summary>
        /// Parmaeter is a number
        /// </summary>
        number,
        /// <summary>
        /// Parameter is a boolean
        /// </summary>
        boolean,
        /// <summary>
        /// Data is an array
        /// </summary>
        array,
        /// <summary>
        /// Data is an object
        /// </summary>
        @object
    }


    /// <summary>
    /// Represents a base class for swagger schema elements
    /// </summary>
    [JsonObject(nameof(SwaggerSchemaElement))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerSchemaElement
    {

        /// <summary>
        /// Type mapping
        /// </summary>
        internal static readonly Dictionary<Type, SwaggerSchemaElementType> m_typeMap = new Dictionary<Type, SwaggerSchemaElementType>()
        {
            {  typeof(int), SwaggerSchemaElementType.number },
            {  typeof(float), SwaggerSchemaElementType.number },
            {  typeof(double), SwaggerSchemaElementType.number },
            {  typeof(long), SwaggerSchemaElementType.number },
            {  typeof(short), SwaggerSchemaElementType.number },
            {  typeof(byte), SwaggerSchemaElementType.number },
            {  typeof(decimal), SwaggerSchemaElementType.number },
            {  typeof(DateTime), SwaggerSchemaElementType.@string},
            {  typeof(DateTimeOffset), SwaggerSchemaElementType.@string},
            {  typeof(Guid), SwaggerSchemaElementType.@string },
            {  typeof(bool), SwaggerSchemaElementType.boolean },
            {  typeof(String), SwaggerSchemaElementType.@string }
        };

        internal static readonly Dictionary<Type, SwaggerSchemaElementFormat?> m_formatMap = new Dictionary<Type, SwaggerSchemaElementFormat?>()
        {
            {  typeof(int), SwaggerSchemaElementFormat.int32 },
            {  typeof(float), SwaggerSchemaElementFormat.@float },
            {  typeof(double), SwaggerSchemaElementFormat.@double },
            {  typeof(long), SwaggerSchemaElementFormat.int64 },
            {  typeof(short), null },
            {  typeof(byte), SwaggerSchemaElementFormat.@byte },
            {  typeof(decimal), SwaggerSchemaElementFormat.@double },
            {  typeof(DateTime), SwaggerSchemaElementFormat.date },
            {  typeof(DateTimeOffset), SwaggerSchemaElementFormat.date },
            {  typeof(Guid), SwaggerSchemaElementFormat.uuid },
            {  typeof(bool), null },
            {  typeof(String), null }
        };

        /// <summary>
        /// Create a new schema element
        /// </summary>
        public SwaggerSchemaElement()
        {

        }

        /// <summary>
        /// Copy a schema element
        /// </summary>
        public SwaggerSchemaElement(SwaggerSchemaElement copy)
        {
            this.Description = copy.Description;
            this.Type = copy.Type;
            this.Required = copy.Required;

            if (copy.Enum != null)
            {
                this.Enum = new List<string>(copy.Enum);
            }

            if (copy.Schema != null)
            {
                this.Schema = new SwaggerSchemaDefinition(copy.Schema);
            }
        }

        /// <summary>
        /// Create a schema element from a property
        /// </summary>
        public SwaggerSchemaElement(PropertyInfo property)
        {
            this.Description = MetadataComposerUtil.GetElementDocumentation(property);

            SwaggerSchemaElementType type = SwaggerSchemaElementType.@string;
            if (property.PropertyType.StripNullable().IsEnum)
            {
                this.Enum = property.PropertyType.StripNullable().GetFields().Select(f => f.GetCustomAttributes<XmlEnumAttribute>().FirstOrDefault()?.Name).Where(o => !string.IsNullOrEmpty(o)).ToList();
                if (this.Enum.Count == 0)
                {
                    this.Enum = property.PropertyType.StripNullable().GetFields().Select(f => f.Name).Where(o => o != "value__").ToList();

                }
                this.Type = SwaggerSchemaElementType.@string;
            }
            else if (typeof(IList).IsAssignableFrom(property.PropertyType) || property.PropertyType.IsArray) // List or array {
            {
                this.Type = SwaggerSchemaElementType.array;

                Type elementType = null;
                if (property.PropertyType.IsArray)
                {
                    elementType = property.PropertyType.GetElementType();
                }
                else if (property.PropertyType.IsConstructedGenericType)
                {
                    elementType = property.PropertyType.GetGenericArguments()[0];
                }

                if (elementType == null || !m_typeMap.TryGetValue(elementType.StripNullable(), out type))
                {
                    this.Items = new SwaggerSchemaDefinition()
                    {
                        Type = SwaggerSchemaElementType.@object,
                        Reference = elementType != null ? $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(elementType)}" : null,
                        NetType = elementType
                    };
                }
                else
                {
                    this.Items = new SwaggerSchemaDefinition()
                    {
                        Type = type,
                        Format = m_formatMap[elementType.StripNullable()]
                    };
                }
            }
            else if (!m_typeMap.TryGetValue(property.PropertyType.StripNullable(), out type))
            {
                this.Schema = new SwaggerSchemaDefinition()
                {
                    Reference = $"#/definitions/{MetadataComposerUtil.CreateSchemaReference(property.PropertyType)}",
                    NetType = property.PropertyType
                };
            }
            else
            {
                this.Type = type;
                this.Format = m_formatMap[property.PropertyType.StripNullable()];
            }
            // XML info
            var xmlElement = property.GetCustomAttributes<XmlElementAttribute>().FirstOrDefault();
            var xmlAttribute = property.GetCustomAttribute<XmlAttributeAttribute>();
            if (xmlElement != null)
            {
                this.Xml = new SwaggerXmlInfo(xmlElement);
            }
            else if (xmlAttribute != null)
            {
                this.Xml = new SwaggerXmlInfo(xmlAttribute);
            }
        }

        /// <summary>
        /// Create schema element from property description
        /// </summary>
        public SwaggerSchemaElement(ResourcePropertyDescription description)
        {
            this.Description = description.Name;
            if (typeof(ResourceDescription) == description.Type)
            {
                this.Type = SwaggerSchemaElementType.@object;
                this.Schema = new SwaggerSchemaDefinition(description.ResourceType);
            }
            else
            {
                this.Type = m_typeMap[description.Type];
            }
        }

        /// <summary>
        /// Create a schema element from resource description
        /// </summary>
        public SwaggerSchemaElement(ResourceDescription description)
        {
            this.Description = description.Description;
            this.Schema = new SwaggerSchemaDefinition(description);
        }

        /// <summary>
        /// Gets or sets the XML information
        /// </summary>
        [JsonProperty("xml")]
        public SwaggerXmlInfo Xml { get; set; }

        /// <summary>
        /// Gets or sets the description 
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the type of the element
        /// </summary>
        [JsonProperty("type")]
        public SwaggerSchemaElementType? Type { get; set; }

        /// <summary>
        /// Gets or sets the type of the element
        /// </summary>
        [JsonProperty("format")]
        public SwaggerSchemaElementFormat? Format { get; set; }


        /// <summary>
        /// Gets whether the parameter is required
        /// </summary>
        [JsonProperty("required")]
        public bool? Required { get; set; }

        /// <summary>
        /// Gets or sets the schema of the element
        /// </summary>
        [JsonProperty("schema")]
        public SwaggerSchemaDefinition Schema { get; set; }

        /// <summary>
        /// Enumerated types
        /// </summary>
        [JsonProperty("enum")]
        public List<string> Enum { get; set; }

        /// <summary>
        /// Items for reference
        /// </summary>
        [JsonProperty("items")]
        public SwaggerSchemaDefinition Items { get; set; }

    }
}