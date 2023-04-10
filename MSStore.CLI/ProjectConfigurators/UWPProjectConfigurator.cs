// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class UWPProjectConfigurator : FileProjectConfigurator
    {
        protected IExternalCommandExecutor ExternalCommandExecutor { get; }
        protected IAppXManifestManager AppXManifestManager { get; }
        protected INuGetPackageManager NuGetPackageManager { get; }

        public UWPProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, INuGetPackageManager nuGetPackageManager, IAppXManifestManager appXManifestManager, ILogger<UWPProjectConfigurator> logger)
            : base(browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
            ExternalCommandExecutor = externalCommandExecutor ?? throw new ArgumentNullException(nameof(externalCommandExecutor));
            NuGetPackageManager = nuGetPackageManager ?? throw new ArgumentNullException(nameof(nuGetPackageManager));
            AppXManifestManager = appXManifestManager ?? throw new ArgumentNullException(nameof(appXManifestManager));
        }

        public override string ConfiguratorProjectType { get; } = "UWP";

        public override string[] SupportedProjectPattern { get; } = new[] { "Package.appxmanifest" };

        public override string[] PackageFilesExtensionInclude => new[] { ".msixupload", ".appxupload" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.Newest;
        public override string OutputSubdirectory { get; } = Path.Combine("obj", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = "AppPackages";
        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64, BuildArch.Arm64 };

        public override bool PackageOnlyOnWindows => true;

        public override AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; } = new[]
        {
            AllowTargetFutureDeviceFamily.Desktop,
            AllowTargetFutureDeviceFamily.Mobile,
            AllowTargetFutureDeviceFamily.Holographic
        };

        public override Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            AppXManifestManager.UpdateManifest(manifestFile.FullName, app, publisherDisplayName, version);

            AnsiConsole.WriteLine($"{ConfiguratorProjectType} project at '{projectRootPath.FullName}' is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your UWP project to the Microsoft Store, see [link]https://learn.microsoft.com/windows/msix/package/packaging-uwp-apps[/]");

            return Task.FromResult((0, output));
        }

        protected Task<bool> BaseCanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            return base.CanConfigureAsync(pathOrUrl, ct);
        }

        public override async Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            if (!await base.CanConfigureAsync(pathOrUrl, ct))
            {
                return false;
            }

            (_, FileInfo manifestFile) = GetInfo(pathOrUrl);

            return !await IsWinUI3Async(manifestFile, ExternalCommandExecutor, NuGetPackageManager, Logger, ct);
        }

        public override Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (_, manifestFile) = GetInfo(pathOrUrl);

            var allImagesInManifest = AppXManifestManager.GetAllImagesFromManifest(manifestFile, Logger);

            return Task.FromResult<List<string>?>(allImagesInManifest);
        }

        public override Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            // Already covered by the default UWP images from the Windows SDK
            return Task.FromResult<List<string>?>(null);
        }

        [SupportedOSPlatform("windows")]
        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            (DirectoryInfo projectRootPath, FileInfo manifestFile) = GetInfo(pathOrUrl);

            return await PackageAsync(projectRootPath, buildArchs, null, PackageFilesExtensionInclude, manifestFile, version, output, ExternalCommandExecutor, AppXManifestManager, Logger, ct);
        }

        [SupportedOSPlatform("windows")]
        internal static async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(DirectoryInfo projectRootPath, IEnumerable<BuildArch>? buildArchs, FileInfo? solutionPath, string[] packageFilesExtensionInclude, FileInfo appxManifestFile, Version? version, DirectoryInfo? output, IExternalCommandExecutor externalCommandExecutor, IAppXManifestManager appXManifestManager, ILogger logger, CancellationToken ct)
        {
            var workingDirectory = solutionPath?.Directory?.FullName ?? projectRootPath.FullName;
            var msbuildPath = await GetMSBuildPathAsync(externalCommandExecutor, logger, workingDirectory, ct);

            if (string.IsNullOrEmpty(msbuildPath))
            {
                return (-1, null);
            }

            await RestorePackagesAsync(externalCommandExecutor, logger, workingDirectory, msbuildPath, ct);

            output ??= new DirectoryInfo(Path.Combine(projectRootPath.FullName, "AppPackages"));

            version = appXManifestManager.UpdateManifestVersion(appxManifestFile.FullName, version);

            var bundleUploadFile = await AnsiConsole.Status().StartAsync("Building MSIX...", async ctx =>
            {
                try
                {
                    string platform;
                    string appxBundlePlatforms;
                    if (buildArchs?.Any() == true)
                    {
                        platform = buildArchs.First().ToString().ToUpperInvariant();
                        appxBundlePlatforms = string.Join("|", buildArchs.Select(a => a.ToString().ToUpperInvariant()));
                    }
                    else
                    {
                        platform = "X64";
                        appxBundlePlatforms = "X64|ARM64";
                    }

                    var escapedOutput = output.FullName.Replace(" ", "%20");

                    var msBuildParams = $"/p:Configuration=Release;AppxBundle=Always;Platform={platform};AppxBundlePlatforms=\"{appxBundlePlatforms}\";AppxPackageDir={escapedOutput}\\;UapAppxPackageBuildMode=StoreUpload";
                    var result = await externalCommandExecutor.RunAsync($"(\"{msbuildPath}\"", $"{msBuildParams})", workingDirectory, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    var bundleUploadFile = result
                        .StdOut
                        .Split(Environment.NewLine)
                        .FirstOrDefault(l =>
                        {
                            foreach (string extension in packageFilesExtensionInclude)
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
                        throw new MSStoreException($"Could not find any file with extensions {string.Join(", ", packageFilesExtensionInclude.Select(e => $"'{e}'"))}!");
                    }

                    ctx.SuccessStatus("MSIX built successfully!");

                    return bundleUploadFile;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to build MSIX.");
                    throw new MSStoreException("Failed to build MSIX.", ex);
                }
            });

            return (0, bundleUploadFile != null ? new FileInfo(bundleUploadFile).Directory : null);
        }

        [SupportedOSPlatform("windows")]
        protected static string GetVSWherePath()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        }

        private static string? _msBuildPath;

        internal static void ResetMSBuildPath()
        {
            _msBuildPath = null;
        }

        [SupportedOSPlatform("windows")]
        protected static async Task<string?> GetMSBuildPathAsync(IExternalCommandExecutor externalCommandExecutor, ILogger logger, string workingDirectory, CancellationToken ct)
        {
            if (_msBuildPath != null)
            {
                return _msBuildPath;
            }

            var vswhere = GetVSWherePath();

            if (!File.Exists(vswhere))
            {
                AnsiConsole.MarkupLine("[red]Visual Studio 2017 or later is required to package UWP apps[/]");
                return null;
            }

            return await AnsiConsole.Status().StartAsync("Finding MSBuild...", async ctx =>
            {
                try
                {
                    var result = await externalCommandExecutor.RunAsync($"\"{vswhere}\"", "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe", workingDirectory, ct);
                    if (result.ExitCode == 0 && result.StdOut.Contains("MSBuild.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.SuccessStatus("Found MSBuild.");
                        _msBuildPath = result.StdOut.Replace(Environment.NewLine, string.Empty);
                        return _msBuildPath;
                    }

                    AnsiConsole.MarkupLine("[red]Could not find MSBuild.[/]");

                    return null;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not find MSBuild.");
                    throw new MSStoreException("Could not find MSBuild.");
                }
            });
        }

        private static Dictionary<string, bool> _nugetRestoreExecuted = new Dictionary<string, bool>();
        [SupportedOSPlatform("windows")]
        protected static async Task RestorePackagesAsync(IExternalCommandExecutor externalCommandExecutor, ILogger logger, string workingDirectory, string msbuildPath, CancellationToken ct)
        {
            if (_nugetRestoreExecuted.TryGetValue(workingDirectory, out var value) && value)
            {
                logger.LogInformation("Using cache. NuGet restore already executed for {WorkingDirectory}.", workingDirectory);
                return;
            }

            await AnsiConsole.Status().StartAsync("Restoring packages...", async ctx =>
            {
                try
                {
                    var msBuildParams = $"/t:restore /p:PublishReadyToRun=true";
                    var result = await externalCommandExecutor.RunAsync($"\"{msbuildPath}\"", msBuildParams, workingDirectory, ct);
                    if (result.ExitCode != 0)
                    {
                        throw new MSStoreException(result.StdErr);
                    }

                    _nugetRestoreExecuted[workingDirectory] = true;

                    ctx.SuccessStatus("Packages restored successfully!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to restore packages.");
                    throw new MSStoreException("Failed to restore packages.");
                }
            });
        }

        private static Dictionary<string, bool> _nuGetExistsExecuted = new Dictionary<string, bool>();

        [SupportedOSPlatform("windows")]
        internal static async Task<bool> IsWinUI3Async(FileInfo appxManifestFile, IExternalCommandExecutor externalCommandExecutor, INuGetPackageManager nuGetPackageManager, ILogger logger, CancellationToken ct)
        {
            if (appxManifestFile.Directory?.FullName == null)
            {
                return false;
            }

            var msbuildPath = await GetMSBuildPathAsync(externalCommandExecutor, logger, appxManifestFile.Directory.FullName, ct);

            if (string.IsNullOrEmpty(msbuildPath))
            {
                return false;
            }

            await RestorePackagesAsync(externalCommandExecutor, logger, appxManifestFile.Directory.FullName, msbuildPath, ct);

            if (appxManifestFile.Directory?.Exists != true)
            {
                return false;
            }

            if (_nuGetExistsExecuted.TryGetValue(appxManifestFile.Directory.FullName, out var value) && value)
            {
                logger.LogInformation("Using cache. {Directory} was already checked for presence of WinUI package.", appxManifestFile.Directory.FullName);
                return true;
            }

            _nuGetExistsExecuted[appxManifestFile.Directory.FullName] = await nuGetPackageManager.IsPackageInstalledAsync(appxManifestFile.Directory, "Microsoft.WindowsAppSDK", ct);

            return _nuGetExistsExecuted[appxManifestFile.Directory.FullName];
        }

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult(AppXManifestManager.GetAppId(fileInfo));
        }
    }
}
