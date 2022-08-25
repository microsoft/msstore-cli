// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using MSStore.CLI.Services.TokenManager;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeTokenManager : ITokenManager
    {
        public Task ClearAllCacheAsync()
        {
            return Task.CompletedTask;
        }

        public Task<AuthenticationResult?> GetTokenAsync(string[] scopes, CancellationToken ct)
        {
            return Task.FromResult<AuthenticationResult?>(null);
        }

        public Task SelectAccountAsync(bool notMSA, bool forceSelection, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
