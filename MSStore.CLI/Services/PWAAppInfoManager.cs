// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MSStore.CLI.Services.PWABuilder;

namespace MSStore.CLI.Services
{
    internal class PWAAppInfoManager : IPWAAppInfoManager
    {
        private const string PWAAppInfoJsonFileName = "pwaAppInfo.json";

        public async Task<PWAAppInfo> LoadAsync(string directoryPath, CancellationToken ct)
        {
            try
            {
                var appInfoPath = Path.Combine(directoryPath, PWAAppInfoJsonFileName);
                using var file = File.Open(appInfoPath, FileMode.Open);

                return await JsonSerializer.DeserializeAsync(file, PWAAppInfoSourceGenerationContext.Default.PWAAppInfo, ct)
                    ?? new PWAAppInfo();
            }
            catch (FileNotFoundException)
            {
                return new PWAAppInfo();
            }
        }

        public async Task SaveAsync(PWAAppInfo pwaAppInfo, string directoryPath, CancellationToken ct)
        {
            var appInfoPath = Path.Combine(directoryPath, PWAAppInfoJsonFileName);
            using var file = File.Open(appInfoPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(
                file,
                pwaAppInfo,
                PWAAppInfoSourceGenerationContext.Default.PWAAppInfo,
                ct);
        }
    }
}
