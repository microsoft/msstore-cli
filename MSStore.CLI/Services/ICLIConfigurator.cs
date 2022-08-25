// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface ICLIConfigurator
    {
        Task<bool> ConfigureAsync(bool askConfirmation = true, string? tenantId = null, CancellationToken ct = default);
        Task<bool> ResetAsync(CancellationToken ct = default);
    }
}
