// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal abstract class FileProjectConfigurator(IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, IEnvironmentInformationService environmentInformationService, IAnsiConsole ansiConsole, ILogger logger) : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
        private readonly IZipFileManager _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
        private readonly IFileDownloader _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
        private readonly IAzureBlobManager _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
        private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new ArgumentNullException(nameof(environmentInformationService));

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        protected IAnsiConsole ErrorAnsiConsole { get; } = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));

        protected ILogger Logger => _logger;

        public abstract string[] SupportedProjectPattern { get; }

        public abstract string[] PackageFilesExtensionInclude { get; }

        public abstract string[]? PackageFilesExtensionExclude { get; }

        public abstract SearchOption PackageFilesSearchOption { get; }

        public abstract PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; }

        public abstract string OutputSubdirectory { get; }

        public abstract string DefaultInputSubdirectory { get; }

        public abstract IEnumerable<BuildArch>? DefaultBuildArchs { get; }

        public abstract bool PackageOnlyOnWindows { get; }

        public abstract AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; }

        public abstract Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct);
        public abstract Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct);

        public virtual Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathOrUrl))
            {
                return Task.FromResult(false);
            }

            try
            {
                DirectoryInfo directoryPath = new DirectoryInfo(pathOrUrl);
                return Task.FromResult(SupportedProjectPattern.Any(y => directoryPath.GetFiles(y).Length != 0));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        protected (DirectoryInfo projectRootPath, FileInfo projectFile) GetInfo(string pathOrUrl)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            FileInfo[] projectFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (projectFiles.Length == 0)
            {
                throw new InvalidOperationException($"No '{SupportedProjectPattern.First()}' file found in the project root directory.");
            }

            var projectFile = projectFiles.First();

            return (projectRootPath, projectFile);
        }

        internal static FileInfo GetAppXManifest(DirectoryInfo projectRootPath)
        {
            return FindFile(projectRootPath, "Package.appxmanifest");
        }

        protected static FileInfo FindFile(DirectoryInfo projectRootPath, string searchPattern)
        {
            var files = projectRootPath.GetFiles(searchPattern, SearchOption.AllDirectories).ToList();

            var rootDirectoriesToIgnore = new string[]
            {
                "node_modules",
                "obj",
                "bin"
            };

            foreach (var ignoreDirectory in rootDirectoriesToIgnore)
            {
                var nodeModules = projectRootPath.GetDirectories(ignoreDirectory, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (nodeModules != null)
                {
                    files = files.Where(f => !f.FullName.StartsWith(nodeModules.FullName, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            if (files.Count == 0)
            {
                throw new InvalidOperationException($"No '{searchPattern}' file found.");
            }

            return files.First();
        }

        public abstract Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct);

        public int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish)
        {
            return null;
        }

        public abstract Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct);

        public abstract Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct);

        private Task<(string, List<SubmissionImage>)> GetFirstSubmissionDataAsync(string listingLanguage, CancellationToken ct)
        {
            var description = $"My {ToString()} App";
            var images = new List<SubmissionImage>();
            return Task.FromResult<(string, List<SubmissionImage>)>((description, images));
        }

        public virtual async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, string? flightId, DirectoryInfo? inputDirectory, bool noCommit, float? packageRolloutPercentage, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, projectFile) = GetInfo(pathOrUrl);

            // Try to find AppId inside the manifestFile/projectFile file
            app = await storePackagedAPI.EnsureAppInitializedAsync(ErrorAnsiConsole, app, projectFile, this, ct);

            if (app?.Id == null)
            {
                return -1;
            }

            ErrorAnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            if (inputDirectory == null)
            {
                inputDirectory = GetInputDirectory(projectRootPath);
            }

            if (!inputDirectory.Exists)
            {
                ErrorAnsiConsole.MarkupLine($"[red bold]Input directory does not exist: {inputDirectory.FullName}[/]");
                ErrorAnsiConsole.MarkupLine($"[red]Make sure you build/package the project before trying to publish it.[/]");
                return -2;
            }

            var output = projectRootPath.CreateSubdirectory(OutputSubdirectory);

            var packageFiles = inputDirectory.GetFiles("*.*", PackageFilesSearchOption)
                .Where(f => PackageFilesExtensionInclude.Contains(f.Extension, StringComparer.OrdinalIgnoreCase)
                    && PackageFilesExtensionExclude?.All(e => !f.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase)) != false);

            if (PublishFileSearchFilterStrategy == PublishFileSearchFilterStrategy.Newest)
            {
                packageFiles = packageFiles.OrderByDescending(f => f.LastWriteTimeUtc).Take(1);
            }
            else if (PublishFileSearchFilterStrategy == PublishFileSearchFilterStrategy.OneLevelDown)
            {
                packageFiles = packageFiles.Where(f => f.Directory?.Parent?.FullName == inputDirectory.FullName);
            }

            Logger.LogInformation("Trying to publish these {FileCount} files: {FileNames}", packageFiles.Count(), string.Join(", ", packageFiles.Select(f => $"'{f.FullName}'")));

            return await storePackagedAPI.PublishAsync(ErrorAnsiConsole, app, flightId, GetFirstSubmissionDataAsync, AllowTargetFutureDeviceFamilies, output, packageFiles, noCommit, packageRolloutPercentage, _browserLauncher, _consoleReader, _zipFileManager, _fileDownloader, _azureBlobManager, _environmentInformationService, _logger, ct);
        }

        protected virtual DirectoryInfo GetInputDirectory(DirectoryInfo projectRootPath)
        {
            return new DirectoryInfo(Path.Combine(projectRootPath.FullName, DefaultInputSubdirectory));
        }

        public Task<bool> CanPublishAsync(string pathOrUrl, CancellationToken ct)
        {
            return CanConfigureAsync(pathOrUrl, ct);
        }
    }
}