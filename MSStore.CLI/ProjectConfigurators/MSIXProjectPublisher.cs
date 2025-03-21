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
    internal class MSIXProjectPublisher(IBrowserLauncher browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, IEnvironmentInformationService environmentInformationService, IAppXManifestManager appXManifestManager, IAnsiConsole ansiConsole, ILogger<MSIXProjectPublisher> logger) : IProjectPublisher
    {
        public string[] PackageFilesExtensionInclude =>
        [
            ".msix",
            ".msixbundle",
            ".msixupload"
        ];

        public override string ToString() => "MSIX";

        public string[]? PackageFilesExtensionExclude { get; }

        public SearchOption PackageFilesSearchOption { get; } = SearchOption.TopDirectoryOnly;

        public AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; } =
        [
            AllowTargetFutureDeviceFamily.Desktop
        ];

        private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
        private readonly IZipFileManager _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
        private readonly IFileDownloader _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
        private readonly IAzureBlobManager _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
        private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new ArgumentNullException(nameof(environmentInformationService));
        private readonly IAppXManifestManager _appXManifestManager = appXManifestManager ?? throw new ArgumentNullException(nameof(appXManifestManager));
        private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private DirectoryInfo? _tempExtractDir;
        private DevCenterApplication? _app;

        public Task<bool> CanPublishAsync(string pathOrUrl, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathOrUrl))
            {
                return Task.FromResult(false);
            }

            try
            {
                FileInfo filePath = new FileInfo(pathOrUrl);
                return Task.FromResult(PackageFilesExtensionInclude.Any(y => y.Equals(filePath.Extension, StringComparison.Ordinal)));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            if (fileInfo == null || fileInfo.Directory == null)
            {
                return Task.FromResult<string?>(null);
            }

            if (_app?.Id != null)
            {
                return Task.FromResult<string?>(_app.Id);
            }

            if (_tempExtractDir == null)
            {
                _tempExtractDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MSStore", "MSIXExtract", Path.GetFileNameWithoutExtension(Path.GetRandomFileName())));
                _zipFileManager.ExtractZip(fileInfo.FullName, _tempExtractDir.FullName);
            }

            var appxManifest = _tempExtractDir.GetFiles("AppxManifest.xml", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (appxManifest == null)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult(_appXManifestManager.GetAppId(appxManifest));
        }

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, string? flightId, DirectoryInfo? inputDirectory, bool noCommit, float? packageRolloutPercentage, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var msix = new FileInfo(pathOrUrl);

            _app = app;

            // Try to find AppId inside the manifestFile/projectFile file
            _app = await storePackagedAPI.EnsureAppInitializedAsync(_ansiConsole, _app, msix, this, ct);

            if (_app?.Id == null)
            {
                return -1;
            }

            _ansiConsole.MarkupLine($"AppId: [green bold]{_app.Id}[/]");

            if (inputDirectory == null)
            {
                inputDirectory = msix.Directory;
            }

            if (inputDirectory?.Exists != true)
            {
                _ansiConsole.MarkupLine($"[red bold]Input directory does not exist: {inputDirectory?.FullName ?? "empty"}[/]");
                _ansiConsole.MarkupLine($"[red]Make sure you build/package the project before trying to publish it.[/]");
                return -2;
            }

            var output = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MSStore", "TempUpload", Path.GetFileNameWithoutExtension(Path.GetRandomFileName())));

            var packageFiles = new List<FileInfo>
            {
                msix
            };

            _logger.LogInformation("Trying to publish these {FileCount} files: {FileNames}", packageFiles.Count, string.Join(", ", packageFiles.Select(f => $"'{f.FullName}'")));

            return await storePackagedAPI.PublishAsync(_ansiConsole, _app, flightId, GetFirstSubmissionDataAsync, AllowTargetFutureDeviceFamilies, output, packageFiles, noCommit, packageRolloutPercentage, _browserLauncher, _consoleReader, _zipFileManager, _fileDownloader, _azureBlobManager, _environmentInformationService, _logger, ct);
        }

        private Task<(string Description, List<SubmissionImage> Images)> GetFirstSubmissionDataAsync(string listingLanguage, CancellationToken ct)
        {
            throw new NotImplementedException("This seems to be your first submission for this app. We don't support first submission loose MSIX files.");
        }
    }
}