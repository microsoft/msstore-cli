// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services.ElectronManager
{
    internal class ElectronManifestManager : IElectronManifestManager
    {
        public async Task<ElectronManifest> LoadAsync(FileInfo manifestFileInfo, CancellationToken ct)
        {
            try
            {
                using var file = manifestFileInfo.OpenRead();

                return await JsonSerializer.DeserializeAsync(file, ElectronManifestSourceGenerationContext.GetCustom().ElectronManifest, ct)
                    ?? new ElectronManifest();
            }
            catch (FileNotFoundException)
            {
                return new ElectronManifest();
            }
        }

        public async Task SaveAsync(ElectronManifest electronManifest, FileInfo manifestFileInfo, CancellationToken ct)
        {
            using var file = manifestFileInfo.Open(FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;

            await JsonSerializer.SerializeAsync(
                file,
                electronManifest,
                ElectronManifestSourceGenerationContext.GetCustom(true).ElectronManifest,
                ct);
        }
    }
}
