// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MSStore.CLI.Services
{
    internal class NuGetPackageManager : INuGetPackageManager
    {
        private readonly ILogger _logger;

        public NuGetPackageManager(ILogger<NuGetPackageManager> logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsPackageInstalledAsync(DirectoryInfo directory, string packageName, CancellationToken ct)
        {
            var projectAssetsJson = directory.GetFiles(Path.Join("obj", "project.assets.json"), SearchOption.TopDirectoryOnly)?.FirstOrDefault();
            if (projectAssetsJson?.Directory?.FullName != null)
            {
                try
                {
                    var fileContent = await File.ReadAllTextAsync(projectAssetsJson.FullName, ct);
                    if (fileContent.Contains($"\"{packageName}/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not check if project is WinUI, assuming it is not.");
                }
            }

            return false;
        }
    }
}
