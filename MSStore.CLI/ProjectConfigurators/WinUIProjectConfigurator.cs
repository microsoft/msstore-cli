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
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class WinUIProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, INuGetPackageManager nuGetPackageManager, IAppXManifestManager appXManifestManager, IEnvironmentInformationService environmentInformationService, IAnsiConsole ansiConsole, ILogger<WinUIProjectConfigurator> logger) : UWPProjectConfigurator(externalCommandExecutor, browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, nuGetPackageManager, appXManifestManager, environmentInformationService, ansiConsole, logger)
    {
        public override string ToString() => "Windows App SDK/WinUI";

        public override string[] PackageFilesExtensionInclude => [".msixupload", ".appxupload", ".msix"];
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.AllDirectories;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.OneLevelDown;

        public override AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; } =
        [
            AllowTargetFutureDeviceFamily.Desktop,
            AllowTargetFutureDeviceFamily.Mobile,
            AllowTargetFutureDeviceFamily.Holographic
        ];

        public override async Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            if (!await BaseCanConfigureAsync(pathOrUrl, ct))
            {
                return false;
            }

            (_, FileInfo manifestFile) = GetInfo(pathOrUrl);

            return await IsWinUI3Async(ErrorAnsiConsole, manifestFile, ExternalCommandExecutor, NuGetPackageManager, Logger, ct);
        }

        [SupportedOSPlatform("windows")]
        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            (DirectoryInfo projectRootPath, FileInfo manifestFile) = GetInfo(pathOrUrl);

            var workingDirectory = projectRootPath.FullName;

            var msbuildPath = await GetMSBuildPathAsync(ErrorAnsiConsole, ExternalCommandExecutor, Logger, workingDirectory, ct);

            if (string.IsNullOrEmpty(msbuildPath))
            {
                return (-1, null);
            }

            await RestorePackagesAsync(ErrorAnsiConsole, ExternalCommandExecutor, Logger, workingDirectory, msbuildPath, ct);

            output ??= new DirectoryInfo(Path.Combine(projectRootPath.FullName, "AppPackages"));

            version = AppXManifestManager.UpdateManifestVersion(manifestFile.FullName, version);

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

                    var msBuildParamsList = new List<string>();
                    var projectName = manifestFile.Directory?.Name ?? "App";

                    // Revisit when Windows App SDK support msixbundle
                    foreach (var appxBundlePlatform in appxBundlePlatforms.Split("|"))
                    {
                        msBuildParamsList.Add($"/p:Configuration=Release;AppxBundle=Always;Platform={appxBundlePlatform};AppxBundlePlatforms={appxBundlePlatform};AppxPackageDir={escapedOutput}\\;UapAppxPackageBuildMode=StoreUpload;GenerateAppxPackageOnBuild=true;AppxPackageTestDir={escapedOutput}\\{projectName}_{version.ToVersionString()}_{appxBundlePlatform}_Test\\");
                    }

                    ExternalCommandExecutionResult? result = null;
                    foreach (var msBuildParams in msBuildParamsList)
                    {
                        var res = await ExternalCommandExecutor.RunAsync($"(\"{msbuildPath}\"", $"{msBuildParams})", workingDirectory, ct);
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

                    ctx.SuccessStatus(ErrorAnsiConsole, "MSIX built successfully!");

                    return bundleUploadFile;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to build MSIX.");
                    throw new MSStoreException("Failed to build MSIX.", ex);
                }
            });

            return (0, bundleUploadFile != null ? new FileInfo(bundleUploadFile).Directory?.Parent : null);
        }
    }
}
