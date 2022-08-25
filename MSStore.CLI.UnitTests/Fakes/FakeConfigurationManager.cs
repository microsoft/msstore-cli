// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Models;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    internal class FakeConfigurationManager : IConfigurationManager
    {
        private Configurations _defaultConfigurations = new Configurations
        {
            SellerId = 1,
            TenantId = "1",
            ClientId = "testUserName"
        };

        public Configurations Configurations { get; set; } = new Configurations();

        public void FakeLogin()
        {
            Configurations.SellerId = _defaultConfigurations.SellerId;
            Configurations.TenantId = _defaultConfigurations.TenantId;
            Configurations.ClientId = _defaultConfigurations.ClientId;
        }

        public Task<Configurations> ClearAsync(CancellationToken ct)
        {
            Configurations = new Configurations();
            return Task.FromResult(Configurations);
        }

        public Task<Configurations> LoadAsync(bool clearInvalidConfig, CancellationToken ct)
        {
            return Task.FromResult(Configurations);
        }

        public Task SaveAsync(Configurations config, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
