// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace MSStore.CLI.Services
{
    internal class NuGetPackageManager(ILogger<NuGetPackageManager> logger) : INuGetPackageManager
    {
        private readonly ILogger _logger = logger;

        public virtual bool IsMaui(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }

            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            _logger.LogInformation("Checking if project is Maui");

            xmlDoc.Load(fileInfo.FullName);

            var useMauiNode = xmlDoc.SelectSingleNode("/Project/PropertyGroup/UseMaui");

            return useMauiNode?.InnerText?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public virtual async Task<bool> IsPackageInstalledAsync(DirectoryInfo directory, string packageName, CancellationToken ct)
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
