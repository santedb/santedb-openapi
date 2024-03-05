﻿/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{
    /// <summary>
    /// Represents a swagger tag
    /// </summary>
    [JsonObject(nameof(SwaggerTag))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerTag
    {

        /// <summary>
        /// Default constructor for the serialization
        /// </summary>
        public SwaggerTag()
        {

        }

        /// <summary>
        /// Creates a new swagger tag
        /// </summary>
        public SwaggerTag(String name, String description)
        {
            this.Name = name;
            this.Description = description;
        }

        /// <summary>
        /// Gets or sets the description
        /// </summary>
        [JsonProperty("description")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        [JsonProperty("name")]
        public String Name { get; set; }
    }
}