// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.Graph
{
    internal interface IGraphClient
    {
        bool Enabled { get; }
        Task<ListResponse<AzureApplication>> GetAppsByDisplayNameAsync(string displayName, CancellationToken ct);
        Task<AzureApplication> CreateAppAsync(string displayName, CancellationToken ct);
        Task<CreatePrincipalResponse> CreatePrincipalAsync(string appId, CancellationToken ct);
        Task<string> UpdateAppAsync(string id, AppUpdateRequest updatedApp, CancellationToken ct);
        Task<CreateAppSecretResponse> CreateAppSecretAsync(string clientId, string displayName, CancellationToken ct);
    }
}
