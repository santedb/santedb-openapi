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
using RestSrvr.Attributes;
using SanteDB.Rest.Common;
using System;
using System.IO;

namespace SanteDB.Messaging.Metadata.Rest
{
    /// <summary>
    /// Metadata Exchange Service
    /// </summary>
    [ServiceContract(Name = "Metadata Service")]
    public interface IMetadataServiceContract : IRestApiContractImplementation
    {

        /// <summary>
        /// Get OpenAPI Definitions
        /// </summary>
        /// <returns></returns>
        [Get("/swagger-config.json")]
        [ServiceProduces("application/json")]
        Stream GetOpenApiDefinitions();

        /// <summary>
        /// Gets the swagger document for the overall contract
        /// </summary>
        [Get("/{serviceName}/{composer}")]
        [ServiceProduces("application/json")]
        object GetMetadata(String serviceName, String composer);

        /// <summary>
        /// Gets the specified object
        /// </summary>
        [Get("/{*content}")]
        Stream Index(String content);
    }
}
