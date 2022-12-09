// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IConfigurationManager<T>
        where T : new()
    {
        string ConfigPath { get; }
        Task<T> LoadAsync(bool clearInvalidConfig = false, CancellationToken ct = default);
        Task<T> ClearAsync(CancellationToken ct);
        Task SaveAsync(T config, CancellationToken ct);
    }
}
