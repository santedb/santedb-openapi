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
 * Date: 2023-5-19
 */
using Newtonsoft.Json;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Interop.Description;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SanteDB.Messaging.Metadata.Model.Swagger
{
    /// <summary>
    /// Represents a swagger path
    /// </summary>
    [JsonDictionary(nameof(SwaggerPath))]
    [ExcludeFromCodeCoverage] // Serialization class
    public class SwaggerPath : Dictionary<String, SwaggerPathDefinition>
    {

        /// <summary>
        /// Create a new swagger path
        /// </summary>
        public SwaggerPath()
        {

        }

        /// <summary>
        /// Create a copied swagger path
        /// </summary>
        public SwaggerPath(IDictionary<String, SwaggerPathDefinition> copy) : base(copy.ToDictionary(o => o.Key, o => new SwaggerPathDefinition(o.Value)))
        {

        }

        /// <summary>
        /// Create a swagger path via a description
        /// </summary>
        public SwaggerPath(IEnumerable<ServiceOperationDescription> description)
        {
            foreach (var itm in description)
            {
                if (!this.ContainsKey(itm.Verb.ToLower()))
                {
                    this.Add(itm.Verb.ToLower(), new SwaggerPathDefinition(itm));
                }
                else
                {
                    Tracer.GetTracer(typeof(SwaggerPath)).TraceWarning($"Duplicate verb {itm.Verb.ToLower()} on {itm.Path}");
                }
            }
        }
    }
}
