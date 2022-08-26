// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace MSStore.CLI.Services.TokenManager
{
    internal interface ITokenManager
    {
        IAccount? CurrentUser { get; }
        Task SelectAccountAsync(bool notMSA, bool forceSelection, CancellationToken ct);
        Task<AuthenticationResult?> GetTokenAsync(string[] scopes, CancellationToken ct);
        Task ClearAllCacheAsync();
    }
}
