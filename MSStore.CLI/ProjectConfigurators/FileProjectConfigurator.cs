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
    internal abstract class FileProjectConfigurator : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IConsoleReader _consoleReader;
        private readonly IZipFileManager _zipFileManager;
        private readonly IFileDownloader _fileDownloader;
        private readonly IAzureBlobManager _azureBlobManager;

        private readonly ILogger _logger;

        protected ILogger Logger => _logger;

        public FileProjectConfigurator(IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger logger)
        {
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
            _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string ConfiguratorProjectType { get; }

        public abstract string[] SupportedProjectPattern { get; }

        public abstract string[] PackageFilesExtensionInclude { get; }

        public abstract string[]? PackageFilesExtensionExclude { get; }

        public abstract SearchOption PackageFilesSearchOption { get; }

        public abstract PublishFileSearchFilterStrategy PublishFileSearchFilterStrategy { get; }

        public abstract string OutputSubdirectory { get; }

        public abstract string DefaultInputSubdirectory { get; }

        public abstract IEnumerable<BuildArch>? DefaultBuildArchs { get; }

        public virtual Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathOrUrl))
            {
                return Task.FromResult(false);
            }

            try
            {
                DirectoryInfo directoryPath = new DirectoryInfo(pathOrUrl);
                return Task.FromResult(SupportedProjectPattern.Any(y => directoryPath.GetFiles(y).Any()));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        protected (DirectoryInfo projectRootPath, FileInfo projectFiles) GetInfo(string pathOrUrl)
        {
            DirectoryInfo projectRootPath = new DirectoryInfo(pathOrUrl);
            FileInfo[] manifestFiles = projectRootPath.GetFiles(SupportedProjectPattern.First(), SearchOption.TopDirectoryOnly);

            if (manifestFiles.Length == 0)
            {
                throw new InvalidOperationException($"No '{SupportedProjectPattern.First()}' file found in the project root directory.");
            }

            var manifestFile = manifestFiles.First();

            return (projectRootPath, manifestFile);
        }

        public abstract Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct);

        public int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish)
        {
            return null;
        }

        public abstract Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct);

        public abstract Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct);

        private Task<(string?, List<SubmissionImage>)> GetFirstSubmissionDataAsync(string listingLanguage, CancellationToken ct)
        {
            var description = $"My {ConfiguratorProjectType} App";
            var images = new List<SubmissionImage>();
            return Task.FromResult<(string?, List<SubmissionImage>)>((description, images));
        }

        public virtual async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var (projectRootPath, manifestFile) = GetInfo(pathOrUrl);

            // Try to find AppId inside the manifest file
            app = await storePackagedAPI.EnsureAppInitializedAsync(app, manifestFile, this, ct);

            if (app?.Id == null)
            {
                return -1;
            }

            AnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            if (inputDirectory == null)
            {
                inputDirectory = GetInputDirectory(projectRootPath);
            }

            if (!inputDirectory.Exists)
            {
                AnsiConsole.MarkupLine($"[red bold]Input directory does not exist: {inputDirectory.FullName}[/]");
                AnsiConsole.MarkupLine($"[red]Make sure you build/package the project before trying to publish it.[/]");
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

            Logger.LogInformation("Trying to publish these {FileCount} files: {FileNames}", packageFiles.Count(), string.Join(", ", packageFiles.Select(f => $"'{f.FullName}'")));

            return await storePackagedAPI.PublishAsync(app, GetFirstSubmissionDataAsync, output, packageFiles, _browserLauncher, _consoleReader, _zipFileManager, _fileDownloader, _azureBlobManager, _logger, ct);
        }

        protected virtual DirectoryInfo GetInputDirectory(DirectoryInfo projectRootPath)
        {
            return new DirectoryInfo(Path.Combine(projectRootPath.FullName, DefaultInputSubdirectory));
        }
    }
}
