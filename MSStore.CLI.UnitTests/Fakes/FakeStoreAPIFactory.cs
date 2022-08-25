// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeStoreAPIFactory : IStoreAPIFactory
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly ICredentialManager _credentialManager;

        internal FakeStoreAPI FakeStoreAPI { get; private set; } = null!;
        internal FakeStorePackagedAPI FakeStorePackagedAPI { get; private set; } = null!;

        private TaskCompletionSource _tcs = new TaskCompletionSource();

        public FakeStoreAPIFactory(IConfigurationManager configurationManager, ICredentialManager credentialManager)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
        }

        public async Task InitAsync(Configurations? config = null, string? secret = null, CancellationToken ct = default)
        {
            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (string.IsNullOrEmpty(config.ClientId))
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            secret = secret ?? _credentialManager.ReadCredential(config.ClientId);

            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("Invalid credential");
            }

            FakeStoreAPI = new FakeStoreAPI(config);
            FakeStorePackagedAPI = new FakeStorePackagedAPI(config);

            _tcs.TrySetResult();
        }

        public async Task<IStoreAPI> CreateAsync(Configurations? config = null, CancellationToken ct = default)
        {
            await _tcs.Task.ConfigureAwait(false);

            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (string.IsNullOrEmpty(config.ClientId))
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            var secret = _credentialManager.ReadCredential(config.ClientId);
            if (!string.IsNullOrEmpty(secret))
            {
                return FakeStoreAPI!;
            }

            throw new InvalidOperationException("Invalid credential");
        }

        public async Task<IStorePackagedAPI> CreatePackagedAsync(Configurations? config = null, CancellationToken ct = default)
        {
            config = config ?? await _configurationManager.LoadAsync(ct: ct);

            if (string.IsNullOrEmpty(config.ClientId))
            {
                throw new InvalidOperationException("Configuration Client Id is empty.");
            }

            var secret = _credentialManager.ReadCredential(config.ClientId);
            if (!string.IsNullOrEmpty(secret))
            {
                return FakeStorePackagedAPI!;
            }

            throw new InvalidOperationException("Invalid credential");
        }
    }
}
