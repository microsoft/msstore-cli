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

        /// <summary>
        /// Loads the configuration, repairing invalid content but distinguishing a locked file
        /// (contention with another process) from one that was genuinely unreadable, so callers
        /// can avoid making destructive decisions based on a config they never actually read.
        /// </summary>
        /// <returns>The loaded (or default) configuration, and whether it was actually read from disk.</returns>
        Task<(T Configurations, bool Readable)> TryLoadAsync(CancellationToken ct = default);
        Task<T> ClearAsync(CancellationToken ct);
        Task SaveAsync(T config, CancellationToken ct);
    }
}
