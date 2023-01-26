// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class ReactNativeProjectConfigurator : NodeBaseProjectConfigurator
    {
        public ReactNativeProjectConfigurator(IExternalCommandExecutor externalCommandExecutor, IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger<ReactNativeProjectConfigurator> logger)
            : base(externalCommandExecutor, browserLauncher, consoleReader, zipFileManager, fileDownloader, azureBlobManager, logger)
        {
        }

        public override string ConfiguratorProjectType { get; } = "React Native";

        public override string[] PackageFilesExtensionInclude => new[] { ".msixupload", ".appxupload" };
        public override string[]? PackageFilesExtensionExclude { get; }
        public override SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;
        public override PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; } = PublishFileSearchFilterStrategy.Newest;
        public override string OutputSubdirectory { get; } = Path.Combine("windows", "obj", "MSStore.CLI");
        public override string DefaultInputSubdirectory { get; } = "windows";

        protected override DirectoryInfo GetInputDirectory(DirectoryInfo projectRootPath)
        {
            var windowsDirectory = base.GetInputDirectory(projectRootPath);

            var appxManifest = GetAppXManifest(windowsDirectory);

            return appxManifest?.Directory != null
                ? new DirectoryInfo(Path.Combine(appxManifest.Directory.FullName, "AppPackages"))
                : windowsDirectory;
        }

        public override IEnumerable<BuildArch>? DefaultBuildArchs => new[] { BuildArch.X64, BuildArch.Arm64 };

        public override bool PackageOnlyOnWindows => true;

        public override async Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (!await base.CanConfigureAsync(pathOrUrl, ct))
            {
                return false;
            }

            var (projectRootPath, _) = GetInfo(pathOrUrl);

            if (IsYarn(projectRootPath))
            {
                if (!await RunYarnInstallAsync(projectRootPath, ct))
                {
                    return false;
                }

                return await YarnPackageExistsAsync(projectRootPath, "react-native", true, ct);
            }
            else
            {
                if (!await RunNpmInstallAsync(projectRootPath, ct))
                {
                    return false;
                }

                return await NpmPackageExistsAsync(projectRootPath, "react-native", true, ct);
            }
        }

        public override Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, reactNativeProjectFile) = GetInfo(pathOrUrl);
            var appxManifest = GetAppXManifest(projectRootPath);

            UWPProjectConfigurator.UpdateManifest(appxManifest.FullName, app, publisherDisplayName, version);

            AnsiConsole.WriteLine($"React Native project '{reactNativeProjectFile.FullName}', with AppX manifest file at '{appxManifest.FullName}', is now configured to build to the Microsoft Store!");
            AnsiConsole.MarkupLine("For more information on building your React Native project to the Microsoft Store, see [link]https://microsoft.github.io/react-native-windows/docs/app-publishing[/]");

            return Task.FromResult((0, output));
        }

        public override Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (projectRootPath, _) = GetInfo(pathOrUrl);
            var appxManifest = GetAppXManifest(projectRootPath);
            var allImagesInManifest = UWPProjectConfigurator.GetAllImagesFromManifest(appxManifest, Logger);

            return Task.FromResult<List<string>?>(allImagesInManifest);
        }

        public override Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            var (projectRootPath, _) = GetInfo(pathOrUrl);
            var appxAssetsFolder = GetDefaultAssetsAppxFolder(projectRootPath);
            if (Directory.Exists(appxAssetsFolder))
            {
                var appxAssetsDir = new DirectoryInfo(appxAssetsFolder);
                return Task.FromResult<List<string>?>(appxAssetsDir.GetFiles().Select(f => f.FullName).ToList());
            }

            return Task.FromResult<List<string>?>(null);
        }

        private static string? GetDefaultAssetsAppxFolder(DirectoryInfo projectRootPath)
        {
            return Path.Combine(projectRootPath.FullName, "node_modules", "react-native-windows", "template", "shared-app", "assets");
        }

        internal static FileInfo GetAppXManifest(DirectoryInfo projectRootPath)
        {
            return FindFile(projectRootPath, "Package.appxmanifest");
        }

        private static FileInfo FindFile(DirectoryInfo projectRootPath, string searchPattern)
        {
            var files = projectRootPath.GetFiles(searchPattern, SearchOption.AllDirectories).ToList();

            var nodeModules = projectRootPath.GetDirectories("node_modules", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (nodeModules != null)
            {
                files = files.Where(f => !f.FullName.StartsWith(nodeModules.FullName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!files.Any())
            {
                throw new InvalidOperationException($"No '{searchPattern}' file found.");
            }

            return files.First();
        }

        [SupportedOSPlatform("windows")]
        public override async Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, _) = GetInfo(pathOrUrl);

            var windowsDirectory = new DirectoryInfo(Path.Combine(projectRootPath.FullName, "windows"));
            if (!windowsDirectory.Exists)
            {
                throw new InvalidOperationException($"No 'windows' directory found in '{projectRootPath.FullName}'.");
            }

            var appxManifest = GetAppXManifest(windowsDirectory);

            if (appxManifest?.Directory == null)
            {
                return (-1, null);
            }

            var solutionFile = FindFile(windowsDirectory, "*.sln");

            output ??= GetInputDirectory(projectRootPath);

            return await UWPProjectConfigurator.PackageAsync(appxManifest.Directory, buildArchs, solutionFile, PackageFilesExtensionInclude, appxManifest, version, output, ExternalCommandExecutor, Logger, ct);
        }

        public override Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null || fileInfo.Directory == null)
            {
                return Task.FromResult<string?>(null);
            }

            var appxManifest = GetAppXManifest(fileInfo.Directory);

            return Task.FromResult(UWPProjectConfigurator.GetAppId(appxManifest));
        }
    }
}
