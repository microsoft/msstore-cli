// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.CLI.Services.CredentialManager;

namespace MSStore.CLI.Services
{
    internal class StoreAPIFactory : IStoreAPIFactory
    {
        private readonly IConfigurationManager<Configurations> _configurationManager;
        private readonly ICredentialManager _credentialManager;
        private readonly ILogger<StoreAPI> _logger;

        public StoreAPIFactory(IConfigurationManager<Configurations> configurationManager, ICredentialManager credentialManager, ILogger<StoreAPI> logger)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IStoreAPI> CreateAsync(Configurations? config = null, CancellationToken ct = default)
        {
            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (!config.ClientId.HasValue)
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            var secret = _credentialManager.ReadCredential(config.ClientId.Value.ToString());
            if (!string.IsNullOrEmpty(secret))
            {
                var storeAPI = new StoreAPI(
                    config.GetStoreConfigurations(),
                    secret,
                    config.StoreApiServiceUrl,
                    config.StoreApiScope,
                    _logger);

                await storeAPI.InitAsync(ct);

                return storeAPI;
            }

            throw new InvalidOperationException("Invalid credential");
        }

        public async Task<IStorePackagedAPI> CreatePackagedAsync(Configurations? config = null, CancellationToken ct = default)
        {
            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (!config.ClientId.HasValue)
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            var secret = _credentialManager.ReadCredential(config.ClientId.Value.ToString());
            if (!string.IsNullOrEmpty(secret))
            {
                var storePackagedAPI = new StorePackagedAPI(
                    config.GetStoreConfigurations(),
                    secret,
                    config.DevCenterServiceUrl,
                    config.DevCenterScope,
                    _logger);

                await storePackagedAPI.InitAsync(ct);

                return storePackagedAPI;
            }

            throw new InvalidOperationException("Invalid credential");
        }
    }
}
