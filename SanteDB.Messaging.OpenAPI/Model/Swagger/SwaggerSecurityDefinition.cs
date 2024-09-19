/*
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
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{

    /// <summary>
    /// Swagger security type
    /// </summary>
    public enum SwaggerSecurityType
    {
        /// <summary>
        /// The security uses HTTP BASIC
        /// </summary>
        basic,
        /// <summary>
        /// The security is OAUTH
        /// </summary>
        oauth2
    }

    /// <summary>
    /// Represents the swagger security flow
    /// </summary>
    public enum SwaggerSecurityFlow
    {
        /// <summary>
        /// The security flow is PASSWORD grant
        /// </summary>
        password,
        /// <summary>
        /// The security flow permitted is client_Credentials
        /// </summary>
        client_credentials,
        /// <summary>
        /// The security flow permitted is authorization code
        /// </summary>
        authorization_code,
        /// <summary>
        /// The security flow permitted is refresh
        /// </summary>
        refresh
    }

    /// <summary>
    /// Represents a security definition
    /// </summary>
    [JsonObject(nameof(SwaggerSecurityDefinition))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerSecurityDefinition
    {

        /// <summary>
        /// Gets or sets the type
        /// </summary>
        [JsonProperty("type")]
        public SwaggerSecurityType Type { get; set; }

        /// <summary>
        /// Gets or sets the flow control
        /// </summary>
        [JsonProperty("flow")]
        public SwaggerSecurityFlow Flow { get; set; }

        /// <summary>
        /// Gets or sets the token url
        /// </summary>
        [JsonProperty("tokenUrl")]
        public String TokenUrl { get; set; }

        /// <summary>
        /// Gets or set sthe scopes
        /// </summary>
        [JsonProperty("scopes")]
        public Dictionary<String, String> Scopes { get; set; }


    }
}