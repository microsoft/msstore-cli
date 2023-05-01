// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;

namespace MSStore.CLI.Services
{
    internal class EnvironmentInformationService : IEnvironmentInformationService
    {
        private readonly bool _runningOnCI;

        public EnvironmentInformationService(ILogger<EnvironmentInformationService> logger)
        {
            _runningOnCI = false;
            var envVariablesTable = new (string EnvVariable, string Value)[]
            {
                ("CI", "true"),
                ("TF_BUILD", "true")
            };

            try
            {
                foreach (var envVariable in envVariablesTable)
                {
                    var value = Environment.GetEnvironmentVariable(envVariable.EnvVariable);
                    if (value != null && value.Equals(envVariable.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        _runningOnCI = true;
                        logger.LogInformation("Running on CI. {EnvVariable}={Value}", envVariable.EnvVariable, envVariable.Value);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking if running on CI.");
            }
        }

        public bool IsRunningOnCI => _runningOnCI;
    }
}
