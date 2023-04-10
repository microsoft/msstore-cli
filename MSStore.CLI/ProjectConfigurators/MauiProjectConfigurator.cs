// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class MauiProjectConfigurator : FileProjectConfigurator
    {
        private readonly INuGetPackageManager _nuGetPackageManager;
        private readonly IExternalCommandExecutor _externalCommandExecutor;
        private readonly IAppXManifestManager _appXManifestManager;

        public MauiProjectConfigurator(IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, INuGetPackageManager nuGetPackageManager, IExternalCommandExecutor externalCommandExecutor, IAppXManifestManager appXManifestManager, ILogger<MauiProjectConfigurator> logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            _nuGetPackageManager = nuGetPackageManager ?? throw new ArgumentNullException(nameof(nuGetPackageManager));
            _externalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
            _appXManifestManager = appXManifestManager ?? throw new ArgumentNullException(nameof(appXManifestManager));
        }

        public override string ConfiguratorProjectType => "Maui";

        public override string[] SupportedProjectPattern => new[] { "*.csproj" };

        public override string[] PackageFilesExtensionInclude => new[] { ".msix" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.AllDirectories;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.OneLevelDown;
        public override string OutputSubdirectory { get; } = Path.Combine("obj", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = "AppPackages";

        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64 };

        public override bool PackageOnlyOnWindows => true;

        public override AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; } = new[]
        {
            AllowTargetFutureDeviceFamily.Desktop,
            AllowTargetFutureDeviceFamily.Mobile,
            AllowTargetFutureDeviceFamily.Holographic
        };

        public override async Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (!await base.CanConfigureAsync(pathOrUrl, ct))
            {
                return false;
            }

            (_, FileInfo projectFile) = GetInfo(pathOrUrl);

            return _nuGetPackageManager.IsMaui(projectFile);
        }

        public override Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, csProjectFile) = GetInfo(pathOrUrl);
            var appxManifest = GetAppXManifest(projectRootPath);

            _appXManifestManager.MinimalUpdateManifest(appxManifest.FullName, app, publisherDisplayName);

            UpdateCSProj(csProjectFile, app);

            AnsiConsole.WriteLine($"Maui project '{csProjectFile.FullName}', with AppX manifest file at '{appxManifest.FullName}', is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your Maui project to the Microsoft Store, see [link]https://learn.microsoft.com/dotnet/maui/windows/deployment/overview[/]");

            return Task.FromResult((0, output));
        }

        internal static void UpdateCSProj(FileInfo fileInfo, DevCenterApplication app)
        {
            if (!fileInfo.Exists)
            {
                return;
            }

            XmlDocument xmlDoc = new XmlDocument
            {
                PreserveWhitespace = true
            };

            xmlDoc.Load(fileInfo.FullName);

            var applicationTitleNode = xmlDoc.SelectSingleNode("/Project/PropertyGroup/ApplicationTitle");
            if (applicationTitleNode != null)
            {
                applicationTitleNode.InnerText = app.PrimaryName ?? string.Empty;
            }

            var propertyGroups = xmlDoc.SelectNodes("/Project/PropertyGroup");
            XmlNode propertGroup;
            if (propertyGroups != null && propertyGroups.Count > 0)
            {
                propertGroup = propertyGroups[0]!;
            }
            else
            {
                propertGroup = xmlDoc.CreateElement("PropertyGroup");
                xmlDoc.AppendChild(propertGroup);
            }

            XmlElement? applicationIdNode = null;
            var condition = "$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'";

            var applicationIdNodes = xmlDoc.SelectNodes("/Project/PropertyGroup/ApplicationId");
            if (applicationIdNodes != null && applicationIdNodes.Count >= 2)
            {
                applicationIdNode = applicationIdNodes.Cast<XmlElement>()
                    .FirstOrDefault(e =>
                        e.HasAttribute("Condition")
                        && e.Attributes["Condition"]?.InnerText?.Equals(condition, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (applicationIdNode == null)
            {
                applicationIdNode = xmlDoc.CreateElement("ApplicationId");
                propertGroup.AppendChild(applicationIdNode);
            }

            applicationIdNode.SetAttribute("Condition", condition);

            applicationIdNode.InnerText = app.PackageIdentityName ?? string.Empty;

            xmlDoc.Save(fileInfo.FullName);
        }

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null || fileInfo.Directory == null)
            {
                return Task.FromResult<string?>(null);
            }

            var appxManifest = GetAppXManifest(fileInfo.Directory);

            return Task.FromResult(_appXManifestManager.GetAppId(appxManifest));
        }

        public override Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (projectRootPath, _) = GetInfo(pathOrUrl);
            var appxManifest = GetAppXManifest(projectRootPath);

            // This will return only the placeholders. Need to update to look at Maui's folder.
            var allImagesInManifest = _appXManifestManager.GetAllImagesFromManifest(appxManifest, Logger);

            return Task.FromResult<List<string>?>(allImagesInManifest);
        }

        public override Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            // Already covered by the default UWP images from the Windows SDK
            return Task.FromResult<List<string>?>(null);
        }

        private Dictionary<string, bool> _nugetRestoreExecuted = new Dictionary<string, bool>();
        private async Task RestorePackagesAsync(string workingDirectory, CancellationToken ct)
        {
            if (_nugetRestoreExecuted.TryGetValue(workingDirectory, out var value) && value)
            {
                Logger.LogInformation("Using cache. NuGet restore already executed for {WorkingDirectory}.", workingDirectory);
                return;
            }

            await AnsiConsole.Status().StartAsync("Restoring packages...", async ctx =>
            {
                try
                {
                    var msBuildParams = $"restore";
                    var result = await _externalCommandExecutor.RunAsync($"dotnet", msBuildParams, workingDirectory, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    _nugetRestoreExecuted[workingDirectory] = true;

                    ctx.SuccessStatus("Packages restored successfully!");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to restore packages.");
                    throw new MSStoreException("Failed to restore packages.");
                }
            });
        }

        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            (DirectoryInfo projectRootPath, FileInfo csProjectFile) = GetInfo(pathOrUrl);

            var workingDirectory = csProjectFile.Directory?.FullName ?? projectRootPath.FullName;

            await RestorePackagesAsync(workingDirectory, ct);

            output ??= new DirectoryInfo(Path.Combine(projectRootPath.FullName, "AppPackages"));

            // TODO: Don't use the appx manifest version. Switch to the csprojs version.
            // version = _appXManifestManager.UpdateManifestVersion(csProjectFile.FullName, version);
            version ??= new Version(1, 0, 0, 0);

            var bundleUploadFile = await AnsiConsole.Status().StartAsync("Building MSIX...", async ctx =>
            {
                try
                {
                    string appxBundlePlatforms;
                    if (buildArchs?.Any() != true)
                    {
                        buildArchs = DefaultBuildArchs!;
                    }

                    appxBundlePlatforms = string.Join("|", buildArchs.Select(a => a.ToString().ToUpperInvariant()));

                    var escapedOutput = output.FullName.Replace(" ", "%20");

                    var targetFramework = GetWindowsTargetFramework(csProjectFile);
                    if (targetFramework == null)
                    {
                        throw new MSStoreException("Could not find a target framework for Windows");
                    }

                    var msBuildParamsList = new List<string>();
                    var projectName = csProjectFile.Directory?.Name ?? "App";

                    // Revisit when Windows App SDK support msixbundle
                    foreach (var appxBundlePlatform in appxBundlePlatforms.Split("|"))
                    {
                        var runtime = $"win10-{appxBundlePlatform.ToLowerInvariant()}";

                        // Maybe switch AppxPackageTestDir to bin\Release\{targetFramework}\{runtime}\AppPackages\?
                        msBuildParamsList.Add($"publish -f {targetFramework} -r {runtime} --self-contained /p:Configuration=Release;AppxBundle=Always;Platform={appxBundlePlatform};AppxBundlePlatforms={appxBundlePlatform};AppxPackageDir={escapedOutput}\\;UapAppxPackageBuildMode=StoreUpload;GenerateAppxPackageOnBuild=true;AppxPackageTestDir={escapedOutput}\\{projectName}_{version.ToVersionString()}_{appxBundlePlatform}_Test\\");
                    }

                    ExternalCommandExecutionResult? result = null;
                    foreach (var msBuildParams in msBuildParamsList)
                    {
                        var res = await _externalCommandExecutor.RunAsync("dotnet", msBuildParams, workingDirectory, ct);
                        if (res.ExitCode != 0)
                        {
                            throw new MSStoreException(res.StdErr);
                        }

                        result = res;
                    }

                    if (result == null)
                    {
                        throw new MSStoreException("Internal error: result is null!");
                    }

                    var bundleUploadFile = result.Value
                        .StdOut
                        .Split(Environment.NewLine)
                        .FirstOrDefault(l =>
                        {
                            foreach (string extension in PackageFilesExtensionInclude)
                            {
                                if (l.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }

                            return false;
                        })
                        ?.Split("->")
                        ?.Last()
                        ?.Trim();

                    if (bundleUploadFile == null)
                    {
                        throw new MSStoreException($"Could not find any file with extensions {string.Join(", ", PackageFilesExtensionInclude.Select(e => $"'{e}'"))}!");
                    }

                    ctx.SuccessStatus("MSIX built successfully!");

                    return bundleUploadFile;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to build MSIX.");
                    throw new MSStoreException("Failed to build MSIX.", ex);
                }
            });

            return (0, bundleUploadFile != null ? new FileInfo(bundleUploadFile).Directory : null);
        }

        private static string? GetWindowsTargetFramework(FileInfo fileInfo)
        {
            if (!fileInfo.Exists)
            {
                return null;
            }

            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(fileInfo.FullName);

            var targetFrameworksNodes = xmlDoc.SelectNodes("/Project/PropertyGroup/TargetFrameworks");
            if (targetFrameworksNodes != null && targetFrameworksNodes.Count > 0)
            {
                foreach (var targetFrameworksNode in targetFrameworksNodes.Cast<XmlElement>())
                {
                    var targetFrameworks = targetFrameworksNode.InnerText.Split(";");
                    if (targetFrameworks.Any(tf => tf.Contains("windows10", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return targetFrameworks.First(tf => tf.Contains("windows10", StringComparison.InvariantCultureIgnoreCase));
                    }
                }
            }

            return null;
        }
    }
}
