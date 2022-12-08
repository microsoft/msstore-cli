// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.ElectronManager
{
    internal interface IElectronManifestManager
    {
        Task<ElectronManifest> LoadAsync(FileInfo manifestFileInfo, CancellationToken ct);
        Task SaveAsync(ElectronManifest electronManifest, FileInfo manifestFileInfo, CancellationToken ct);
    }
}