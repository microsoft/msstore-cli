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
        /// Loads the configuration, repairing invalid content, but reporting separately when the
        /// file could not be read at all because another process holds it open, so callers can
        /// avoid making destructive decisions based on a configuration they never actually read.
        /// </summary>
        /// <returns>
        /// The loaded (or default) configuration, and whether the stored state is known.
        /// The flag is <see langword="false"/> only when the file could not be opened because
        /// another process holds it. A missing file (created with defaults) and repaired invalid
        /// content both count as known, since in neither case are we discarding a real setting.
        /// </returns>
        Task<(T Configurations, bool Readable)> TryLoadAsync(CancellationToken ct = default);
        Task<T> ClearAsync(CancellationToken ct);
        Task SaveAsync(T config, CancellationToken ct);
    }
}
