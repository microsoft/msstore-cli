// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Models;

namespace MSStore.CLI.Services
{
    internal interface IConfigurationManager
    {
        Task<Configurations> LoadAsync(bool clearInvalidConfig = false, CancellationToken ct = default);
        Task<Configurations> ClearAsync(CancellationToken ct);
        Task SaveAsync(Configurations config, CancellationToken ct);
    }
}
