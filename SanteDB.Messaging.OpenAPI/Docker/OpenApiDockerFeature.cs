using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Docker.Core;
using SanteDB.Messaging.Metadata.Configuration;
using SanteDB.Messaging.Metadata.Rest;
using SanteDB.Rest.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Messaging.Metadata.Docker
{
    /// <summary>
    /// OpenAPI Feature
    /// </summary>
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
                    Name = "META",
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
                restConfiguration.ExternalHostPort = metadataConf.ApiHost = baseSetting;
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
