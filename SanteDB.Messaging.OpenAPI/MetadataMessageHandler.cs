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
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interop;
using SanteDB.Core.Services;
using SanteDB.Messaging.Metadata.Configuration;
using SanteDB.Messaging.Metadata.Rest;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Messaging.Metadata
{
    /// <summary>
    /// Represents the daemon service that starts/stops the OpenApi information file
    /// </summary>
    [Description("Allows SanteDB iCDR/dCDR to expose service metadata using OpenAPI/Swagger 2.0")]
    [ApiServiceProvider("OpenAPI Metadata Exchange", typeof(MetadataServiceBehavior), ServiceEndpointType.Metadata, Configuration = typeof(MetadataConfigurationSection))]
    [ExcludeFromCodeCoverage]
    public class MetadataMessageHandler : IDaemonService, IApiEndpointProvider
    {
        // Trace source for logs
        private readonly Tracer m_traceSource = new Tracer(MetadataConstants.TraceSourceName);

        /// <summary>
        /// Represents a rest service
        /// </summary>
        private RestService m_webHost;

        /// <summary>
        /// Get whether the daemon service is running
        /// </summary>
        public bool IsRunning => this.m_webHost?.IsRunning == true;

        /// <summary>
        /// Get the name of this service
        /// </summary>
        public string ServiceName => "SanteDB Metadata Service";

        /// <summary>
        /// Gets the contract type
        /// </summary>
        public Type BehaviorType => typeof(MetadataServiceBehavior);

        /// <summary>
        /// Gets the API type
        /// </summary>
        public ServiceEndpointType ApiType => ServiceEndpointType.Metadata;

        /// <summary>
        /// URL of the service
        /// </summary>
        public string[] Url => this.m_webHost.Endpoints.OfType<ServiceEndpoint>().Select(o => o.Description.ListenUri.ToString()).ToArray();

        /// <summary>
        /// Get capabilities of this endpoint
        /// </summary>
        public ServiceEndpointCapabilities Capabilities => (ServiceEndpointCapabilities)ApplicationServiceContext.Current.GetService<IRestServiceFactory>().GetServiceCapabilities(this.m_webHost);

        /// <summary>
        /// Configuration service name
        /// </summary>
        public const string ConfigurationName = "META";

        /// <summary>
        /// Default ctor
        /// </summary>
        public MetadataMessageHandler()
        {
        }

        /// <summary>
        /// Fired when the service is starting
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Fired when the service has started
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Fired when the service is stopping
        /// </summary>
        public event EventHandler Stopping;

        /// <summary>
        /// Fired when the service has stopped
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Starts the service and binds the service endpoints
        /// </summary>
        public bool Start()
        {
            this.Starting?.Invoke(this, EventArgs.Empty);

            this.m_webHost = ApplicationServiceContext.Current.GetService<IRestServiceFactory>().CreateService(ConfigurationName);

            // Add service behaviors
            foreach (ServiceEndpoint endpoint in this.m_webHost.Endpoints)
            {
                this.m_traceSource.TraceInfo("Starting MetadataExchange on {0}...", endpoint.Description.ListenUri);
            }

            // Start the webhost
            this.m_webHost.Start();
            this.Started?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        public bool Stop()
        {
            this.Stopping?.Invoke(this, EventArgs.Empty);

            this.m_webHost.Stop();

            this.Stopped?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}