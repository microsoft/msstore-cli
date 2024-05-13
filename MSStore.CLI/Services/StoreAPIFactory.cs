// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StoreAPI> _logger;

        public StoreAPIFactory(IConfigurationManager<Configurations> configurationManager, ICredentialManager credentialManager, IHttpClientFactory httpClientFactory, ILogger<StoreAPI> logger)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
            X509Certificate2? cert = LoadCertificate(config, secret);

            StoreAPI? storeAPI;
            if (cert != null)
            {
                storeAPI = new StoreAPI(
                    config.GetStoreConfigurations(),
                    cert,
                    config.StoreApiServiceUrl,
                    config.StoreApiScope,
                    _logger);
            }
            else if (!string.IsNullOrEmpty(secret))
            {
                storeAPI = new StoreAPI(
                    config.GetStoreConfigurations(),
                    secret,
                    config.StoreApiServiceUrl,
                    config.StoreApiScope,
                    _logger);
            }
            else
            {
                throw new InvalidOperationException("Invalid credential");
            }

            await storeAPI.InitAsync(_httpClientFactory.CreateClient("Default"), ct);

            return storeAPI;
        }

        public async Task<IStorePackagedAPI> CreatePackagedAsync(Configurations? config = null, CancellationToken ct = default)
        {
            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (!config.ClientId.HasValue)
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            var secret = _credentialManager.ReadCredential(config.ClientId.Value.ToString());
            X509Certificate2? cert = LoadCertificate(config, secret);

            StorePackagedAPI? storePackagedAPI;
            if (cert != null)
            {
                storePackagedAPI = new StorePackagedAPI(
                    config.GetStoreConfigurations(),
                    cert,
                    config.DevCenterServiceUrl,
                    config.DevCenterScope,
                    _logger);
            }
            else if (!string.IsNullOrEmpty(secret))
            {
                storePackagedAPI = new StorePackagedAPI(
                    config.GetStoreConfigurations(),
                    secret,
                    config.DevCenterServiceUrl,
                    config.DevCenterScope,
                    _logger);
            }
            else
            {
                throw new InvalidOperationException("Invalid credential");
            }

            await storePackagedAPI.InitAsync(_httpClientFactory.CreateClient("Default"), ct);

            return storePackagedAPI;
        }

        private static X509Certificate2? LoadCertificate(Configurations config, string? secret)
        {
            if (config.CertificateThumbprint != null)
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var certificates = store.Certificates.Find(
                        X509FindType.FindByThumbprint,
                        config.CertificateThumbprint,
                        validOnly: false);

                    if (certificates.Count == 0)
                    {
                        throw new InvalidOperationException($"Certificate not found for {config.CertificateThumbprint}.");
                    }

                    return certificates[0];
                }
            }
            else if (config.CertificateFilePath != null)
            {
                return new X509Certificate2(config.CertificateFilePath, string.IsNullOrEmpty(secret) ? null : secret);
            }

            return null;
        }
    }
}
