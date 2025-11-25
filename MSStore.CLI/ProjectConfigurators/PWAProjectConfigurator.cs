// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.PWABuilder;
using Spectre.Console;

namespace MSStore.CLI.ProjectConfigurators
{
    internal class PWAProjectConfigurator(
        IConsoleReader consoleReader,
        IBrowserLauncher browserLauncher,
        IPWABuilderClient pwaBuilderClient,
        IZipFileManager zipFileManager,
        IAzureBlobManager azureBlobManager,
        IFileDownloader fileDownloader,
        IPWAAppInfoManager pwaAppInfoManager,
        IEnvironmentInformationService environmentInformationService,
        IAnsiConsole ansiConsole,
        ILogger<PWAProjectConfigurator> logger) : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
        private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        private readonly IPWABuilderClient _pwaBuilderClient = pwaBuilderClient ?? throw new ArgumentNullException(nameof(pwaBuilderClient));
        private readonly IZipFileManager _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
        private readonly IAzureBlobManager _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
        private readonly IFileDownloader _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
        private readonly IPWAAppInfoManager _pwaAppInfoManager = pwaAppInfoManager ?? throw new ArgumentNullException(nameof(pwaAppInfoManager));
        private readonly IEnvironmentInformationService _environmentInformationService = environmentInformationService ?? throw new ArgumentNullException(nameof(environmentInformationService));
        private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public override string ToString() => "PWA";

        public string[] PackageFilesExtensionInclude =>
        [
            ".appxbundle",
            ".msixbundle",
            ".msix",
            ".appx"
        ];
        public string[]? PackageFilesExtensionExclude { get; } =
        [
            ".sideload.msix"
        ];
        public SearchOption PackageFilesSearchOption { get; } = SearchOption.AllDirectories;
        public IEnumerable<BuildArch>? DefaultBuildArchs { get; }

        public bool PackageOnlyOnWindows => false;

        public AllowTargetFutureDeviceFamily[] AllowTargetFutureDeviceFamilies { get; } =
        [
            AllowTargetFutureDeviceFamily.Desktop,
            AllowTargetFutureDeviceFamily.Holographic
        ];

        public Task<bool> CanConfigureAsync(string pathOrUrl, CancellationToken ct)
        {
            var uri = GetUri(pathOrUrl);

            return Task.FromResult(uri != null || ContainsPWAAppInfoJson(pathOrUrl));
        }

        private WebManifestJson? _webManifest;

        private static bool ContainsPWAAppInfoJson(string pathOrUrl)
        {
            try
            {
                DirectoryInfo directoryPath = new DirectoryInfo(pathOrUrl);
                return directoryPath.GetFiles("pwaAppInfo.json").Length != 0;
            }
            catch
            {
                return false;
            }
        }

        private static Uri? GetUri(string pathOrUrl)
        {
            Uri? uri = null;
            try
            {
                uri = new Uri(pathOrUrl);
                if (!uri.IsAbsoluteUri || uri.IsFile)
                {
                    uri = null;
                }
            }
            catch
            {
            }

            return uri;
        }

        public int? ValidateCommand(string pathOrUrl, DirectoryInfo? output, bool? commandPackage, bool? commandPublish)
        {
            if (output == null && commandPublish != true)
            {
                _ansiConsole.MarkupLine($":collision: [bold red]For PWAs the init command should output to a specific directory (using the '--output' option), or publish directly to the store using the '--publish' option.[/]");
                return -2;
            }

            return null;
        }

        public async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, Version? version, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var uri = GetUri(pathOrUrl);

            if (uri == null)
            {
                return (-1, null);
            }

            if (string.IsNullOrEmpty(app.Id))
            {
                return (-1, null);
            }

            _ansiConsole.MarkupLine($"You've provided a URL, so we'll use [link]PWABuilder.com[/] to setup your PWA and upload it to the Microsoft Store.");
            _ansiConsole.WriteLine();

            string outputZipPath;
            string fileName;
            if (output == null)
            {
                fileName = Path.GetRandomFileName();
                output = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MSStore", "PWAZips", Path.GetFileNameWithoutExtension(fileName)));
            }
            else
            {
                fileName = Path.GetFileName(output.FullName) ?? throw new NotSupportedException("UNC paths are not supported.");
                if (!output.Exists)
                {
                    output.Create();
                }
            }

            outputZipPath = Path.Combine(output.FullName, Path.ChangeExtension(fileName, "zip"));

            var maxVersion = new Version();
            var submission = await _ansiConsole.Status().StartAsync("Retrieving Submission", async ctx =>
            {
                try
                {
                    if (app.LastPublishedApplicationSubmission?.Id != null)
                    {
                        return await storePackagedAPI.GetSubmissionAsync(app.Id, app.LastPublishedApplicationSubmission.Id, ct);
                    }

                    return null;
                }
                catch (MSStoreHttpException err)
                {
                    if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        ctx.ErrorStatus(_ansiConsole, "Could not find the Application or submission. Please check the ProductId.");
                        _logger.LogError(err, "Could not find the Application or submission. Please check the ProductId.");
                    }
                    else
                    {
                        ctx.ErrorStatus(_ansiConsole, "Error while retrieving submission.");
                        _logger.LogError(err, "Error while retrieving submission for Application.");
                    }

                    return null;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while retrieving submission.");
                    ctx.ErrorStatus(_ansiConsole, err);
                    return null;
                }
            });

            if (submission?.ApplicationPackages != null)
            {
                foreach (var applicationPackage in submission.ApplicationPackages)
                {
                    if (applicationPackage.Version != null)
                    {
                        var packageVersion = new Version(applicationPackage.Version);
                        if (packageVersion > maxVersion)
                        {
                            maxVersion = packageVersion;
                        }
                    }
                }
            }

            // PWABuilder doesn't accept Major = 0
            if (maxVersion.ToString() == "0.0" || maxVersion.Major == 0)
            {
                maxVersion = new Version(1, 0, 0);
            }

            var success = await _ansiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Downloading PWA Bundle From [u]PWABuilder.com[/][/]");

                    try
                    {
                        var classicVersion = version != null ? version : new Version(maxVersion.Major, maxVersion.Minor, maxVersion.Build + 1);
                        var zipVersion = new Version(classicVersion.Major, classicVersion.Minor, classicVersion.Build + 1);
                        await _pwaBuilderClient.GenerateZipAsync(
                            new GenerateZipRequest
                            {
                                Url = uri.ToString(),
                                Name = app.PrimaryName ?? string.Empty,
                                PackageId = app.PackageIdentityName,
                                Version = zipVersion.ToString(),
                                AllowSigning = true,
                                ClassicPackage = new ClassicPackage
                                {
                                    Generate = true,
                                    Version = classicVersion.ToString()
                                },
                                Publisher = new Publisher
                                {
                                    DisplayName = publisherDisplayName,
                                    CommonName = app.PublisherName
                                },
                                ResourceLanguage = "en-us", // TODO: parametrize this
                            },
                            outputZipPath,
                            task,
                            ct);

                        _ansiConsole.MarkupLine($":check_mark_button: [green]PWA Bundle successfully downloaded![/]");
                    }
                    catch (InvalidOperationException err)
                    {
                        _ansiConsole.MarkupLine($"Ops! This doesn't seem like the a valid PWA... For more info, go to [link]https://www.pwabuilder.com/reportcard?site={uri.ToString().EscapeMarkup()}[/]");
                        _logger.LogWarning(err, "Not a valid PWA ({PWAUrl})", uri);
                        _ansiConsole.MarkupLine($":collision: [bold red]Error while downloading PWA zip file.[/]");
                        return false;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while downloading PWA zip file.");
                        _ansiConsole.MarkupLine($":collision: [bold red]Error while downloading PWA zip file.[/]");
                        return false;
                    }

                    return true;
                });

            if (!success || outputZipPath == null)
            {
                return (-1, null);
            }

            var zipDir = Path.GetDirectoryName(outputZipPath) ?? throw new NotSupportedException("UNC paths are not supported.");

            var extractedZipDir = Path.Combine(zipDir, Path.GetFileNameWithoutExtension(outputZipPath) + "_PWABuilderExtractedBundle");

            if (Path.Exists(extractedZipDir))
            {
                Directory.Delete(extractedZipDir, true);
            }

            _zipFileManager.ExtractZip(outputZipPath, extractedZipDir);
            var extractedZipDirInfo = new DirectoryInfo(extractedZipDir);

            _logger.LogInformation("Extracted '{ZipPath}' to: '{ExtractedZipDirFullName}'", outputZipPath, extractedZipDirInfo.FullName);

            await _pwaAppInfoManager.SaveAsync(
                new PWAAppInfo
                {
                    AppId = app.Id,
                    Uri = uri,
                },
                zipDir,
                ct);

            return (0, output);
        }

        public Task<List<string>?> GetAppImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            // TODO: implement
            return Task.FromResult<List<string>?>([]);
        }

        public Task<List<string>?> GetDefaultImagesAsync(string pathOrUrl, CancellationToken ct)
        {
            return Task.FromResult<List<string>?>(null);
        }

        public Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, IEnumerable<BuildArch>? buildArchs, Version? version, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            if (GetUri(pathOrUrl) != null && inputDirectory != null)
            {
                pathOrUrl = inputDirectory.FullName;
            }

            if (pathOrUrl == null)
            {
                return Task.FromResult<(int, DirectoryInfo?)>((-1, null));
            }

            return Task.FromResult((0, (DirectoryInfo?)new DirectoryInfo(pathOrUrl)));
        }

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, string? flightId, DirectoryInfo? inputDirectory, bool noCommit, float? packageRolloutPercentage, bool replacePackages, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            Uri? uri = GetUri(pathOrUrl);

            if (uri != null && inputDirectory != null)
            {
                pathOrUrl = inputDirectory.FullName;
            }

            if (pathOrUrl == null)
            {
                return -1;
            }

            _appId = app?.Id;
            try
            {
                // Try to find AppId inside the pwaAppInfo.json file
                var pwaAppInfo = await _pwaAppInfoManager.LoadAsync(pathOrUrl, ct);

                _appId ??= pwaAppInfo.AppId;
                uri = pwaAppInfo.Uri ?? uri;
                if (_appId == null || uri == null)
                {
                    _ansiConsole.MarkupLine($":collision: [bold red]AppId or Uri is not defined.[/]");
                    _logger.LogError("This folder has not been initialized with a PWA. Could not find the AppId or Uri in the pwaAppInfo.json file.");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                _ansiConsole.MarkupLine($":collision: [bold red]Error while loading pwaAppInfo.json file.[/]");
                _logger.LogError(ex, "Error while loading pwaAppInfo.json file.");
                return -1;
            }

            app = await storePackagedAPI.EnsureAppInitializedAsync(_ansiConsole, app, null, this, ct);

            if (app?.Id == null)
            {
                return -1;
            }

            _ansiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            var output = new DirectoryInfo(pathOrUrl);

            var pwaBuilderExtractedBundle = output.GetDirectories("*_PWABuilderExtractedBundle", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (pwaBuilderExtractedBundle == null)
            {
                return -1;
            }

            var packageFiles = pwaBuilderExtractedBundle.GetFiles("*.*", PackageFilesSearchOption)
                .Where(f => PackageFilesExtensionInclude.Contains(f.Extension, StringComparer.OrdinalIgnoreCase)
                    && PackageFilesExtensionExclude?.All(e => !f.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase)) != false);

            if (packageFiles?.Any() != true)
            {
                _ansiConsole.WriteLine("Couldn't find any package to upload.");
                return -1;
            }

            return await storePackagedAPI.PublishAsync(
                _ansiConsole,
                app,
                flightId,
                (listingLanguage, ct) => GetFirstSubmissionDataAsync(listingLanguage, uri, ct),
                AllowTargetFutureDeviceFamilies,
                output,
                packageFiles,
                noCommit,
                packageRolloutPercentage,
                replacePackages,
                _browserLauncher,
                _consoleReader,
                _zipFileManager,
                _fileDownloader,
                _azureBlobManager,
                _environmentInformationService,
                _logger,
                ct);
        }

        private async Task<(string description, List<SubmissionImage> images)> GetFirstSubmissionDataAsync(string listingLanguage, Uri uri, CancellationToken ct)
        {
            Debug.WriteLine(listingLanguage);

            if (_webManifest == null)
            {
                var webManifestResponse = await _ansiConsole.Status().StartAsync("Fetching WebManifest...", async ctx =>
                {
                    try
                    {
                        return await _pwaBuilderClient.FindWebManifestAsync(uri, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while fetching web manifest.");
                        ctx.ErrorStatus(_ansiConsole, "Error while fetching web manifest.");
                        return null;
                    }
                });

                _webManifest = webManifestResponse?.Content?.Json;
            }

            var images = new List<SubmissionImage>();

            if (_webManifest == null)
            {
                return ("Could not fetch webmanifest description.", images);
            }

            if (_webManifest.Screenshots?.Count > 0)
            {
                foreach (var screenShot in _webManifest.Screenshots)
                {
                    if (screenShot.Src != null)
                    {
                        images.Add(new SubmissionImage(screenShot.Src, SubmissionImageType.Screenshot));
                    }
                }
            }

            if (_webManifest.Icons?.Count > 0)
            {
                // Order by size
                _webManifest.Icons.Sort((a, b) => a.GetSize().LengthSquared().CompareTo(b.GetSize().LengthSquared()));
                var icon = _webManifest.Icons.Last();

                if (icon.Src != null)
                {
                    images.Add(new SubmissionImage(icon.Src, SubmissionImageType.Icon));
                }
            }

            var description = _webManifest.Description;
            if (string.IsNullOrEmpty(description))
            {
                description = await _consoleReader.RequestStringAsync($"\tEnter the description of the listing ({listingLanguage}):", false, ct);
            }

            return (description, images);
        }

        private string? _appId;
        public Task<string?> GetAppIdAsync(FileInfo? fileInfo, CancellationToken ct)
        {
            return Task.FromResult(_appId);
        }

        public Task<bool> CanPublishAsync(string pathOrUrl, CancellationToken ct)
        {
            return CanConfigureAsync(pathOrUrl, ct);
        }
    }
}