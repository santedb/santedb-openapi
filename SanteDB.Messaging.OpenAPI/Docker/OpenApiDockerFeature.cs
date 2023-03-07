/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-5-30
 */
using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Docker.Core;
using SanteDB.Messaging.Metadata.Configuration;
using SanteDB.Messaging.Metadata.Rest;
using SanteDB.Rest.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Messaging.Metadata.Docker
{
    /// <summary>
    /// OpenAPI Feature
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OpenApiDockerFeature : IDockerFeature
    {
        /// <summary>
        /// Setting for listen URI
        /// </summary>
        public const string ListenSetting = "LISTEN";

        /// <summary>
        /// Setting for BASE url
        /// </summary>
        public const string ApiBaseSetting = "BASE";

        /// <summary>
        /// Get the id of the feature
        /// </summary>
        public string Id => "SWAGGER";

        /// <summary>
        /// Get the settings
        /// </summary>
        public IEnumerable<string> Settings => new string[] { ApiBaseSetting, ListenSetting };

        /// <summary>
        /// Configure the setting
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="settings"></param>
        public void Configure(SanteDBConfiguration configuration, IDictionary<string, string> settings)
        {
            var metadataConf = configuration.GetSection<MetadataConfigurationSection>();
            if (metadataConf == null)
            {
                metadataConf = new MetadataConfigurationSection();
                configuration.AddSection(metadataConf);
            }

            var restConfiguration = configuration.GetSection<SanteDB.Rest.Common.Configuration.RestConfigurationSection>();
            if (restConfiguration == null)
            {
                throw new ConfigurationException("Error retrieving REST configuration", configuration);
            }

            var openApiRestConfiguration = restConfiguration.Services.FirstOrDefault(o => o.ServiceType == typeof(IMetadataServiceContract));
            if (openApiRestConfiguration == null) // add fhir rest config
            {
                openApiRestConfiguration = new RestServiceConfiguration()
                {
                    ConfigurationName = MetadataMessageHandler.ConfigurationName,
                    Endpoints = new List<RestEndpointConfiguration>()
                    {
                        new RestEndpointConfiguration()
                        {
                            Address = "http://0.0.0.0:8080/api-docs",
                            ContractXml = typeof(IMetadataServiceContract).AssemblyQualifiedName,
                        }
                    }
                };
                restConfiguration.Services.Add(openApiRestConfiguration);
            }

            if (settings.TryGetValue(ListenSetting, out string listenStr))
            {
                if (!Uri.TryCreate(listenStr, UriKind.Absolute, out Uri listenUri))
                {
                    throw new ArgumentException($"{listenStr} is not a valid URI");
                }
                openApiRestConfiguration.Endpoints.ForEach(ep => ep.Address = listenStr);
            }

            // Client claims?
            if (settings.TryGetValue(ApiBaseSetting, out String baseSetting))
            {
                restConfiguration.ExternalHostPort = baseSetting;
            }

            // Add services
            var serviceConfiguration = configuration.GetSection<ApplicationServiceContextConfigurationSection>().ServiceProviders;
            if (!serviceConfiguration.Any(s => s.Type == typeof(MetadataMessageHandler)))
            {
                serviceConfiguration.Add(new TypeReferenceConfiguration(typeof(MetadataMessageHandler)));
            }
        }
    }
}