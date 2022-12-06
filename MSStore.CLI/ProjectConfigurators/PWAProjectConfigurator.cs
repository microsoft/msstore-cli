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
    internal class PWAProjectConfigurator : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IConsoleReader _consoleReader;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IPWABuilderClient _pwaBuilderClient;
        private readonly IZipFileManager _zipFileManager;
        private readonly IAzureBlobManager _azureBlobManager;
        private readonly IFileDownloader _fileDownloader;
        private readonly IPWAAppInfoManager _pwaAppInfoManager;
        private readonly ILogger _logger;

        public PWAProjectConfigurator(
            IConsoleReader consoleReader,
            IBrowserLauncher browserLauncher,
            IPWABuilderClient pwaBuilderClient,
            IZipFileManager zipFileManager,
            IAzureBlobManager azureBlobManager,
            IFileDownloader fileDownloader,
            IPWAAppInfoManager pwaAppInfoManager,
            ILogger<PWAProjectConfigurator> logger)
        {
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _pwaBuilderClient = pwaBuilderClient ?? throw new ArgumentNullException(nameof(pwaBuilderClient));
            _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
            _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
            _pwaAppInfoManager = pwaAppInfoManager ?? throw new ArgumentNullException(nameof(pwaAppInfoManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ConfiguratorProjectType => "PWA";

        public bool CanConfigure(string pathOrUrl)
        {
            var uri = GetUri(pathOrUrl);

            return uri != null || ContainsPWAAppInfoJson(pathOrUrl);
        }

        private WebManifestJson? _webManifest = null;

        private static bool ContainsPWAAppInfoJson(string pathOrUrl)
        {
            try
            {
                DirectoryInfo directoryPath = new DirectoryInfo(pathOrUrl);
                return directoryPath.GetFiles("pwaAppInfo.json").Any();
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
                AnsiConsole.MarkupLine($":collision: [bold red]For PWAs the init command should output to a specific directory (using the '--output' option), or publish directly to the store using the '--publish' option.[/]");
                return -2;
            }

            return null;
        }

        public async Task<(int returnCode, DirectoryInfo? outputDirectory)> ConfigureAsync(string pathOrUrl, DirectoryInfo? output, string publisherDisplayName, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
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

            AnsiConsole.MarkupLine($"You've provided a URL, so we'll use [link]PWABuilder.com[/] to setup your PWA and upload it to the Microsoft Store.");
            AnsiConsole.WriteLine();

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
            var submission = await AnsiConsole.Status().StartAsync("Retrieving Submission", async ctx =>
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
                        ctx.ErrorStatus("Could not find the Application or submission. Please check the ProductId.");
                        _logger.LogError(err, "Could not find the Application or submission. Please check the ProductId.");
                    }
                    else
                    {
                        ctx.ErrorStatus("Error while retrieving submission.");
                        _logger.LogError(err, "Error while retrieving submission for Application.");
                    }

                    return null;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while retrieving submission.");
                    ctx.ErrorStatus(err);
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

            var success = await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Downloading PWA Bundle From [u]PWABuilder.com[/][/]");

                    try
                    {
                        var classicVersion = new Version(maxVersion.Major, maxVersion.Minor, maxVersion.Build + 1);
                        var version = new Version(maxVersion.Major, maxVersion.Minor, maxVersion.Build + 2);
                        await _pwaBuilderClient.GenerateZipAsync(
                            new GenerateZipRequest
                            {
                                Url = uri.ToString(),
                                Name = app.PrimaryName ?? string.Empty,
                                PackageId = app.PackageIdentityName,
                                Version = version.ToString(),
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

                        AnsiConsole.MarkupLine($":check_mark_button: [green]PWA Bundle successfully downloaded![/]");
                    }
                    catch (InvalidOperationException err)
                    {
                        AnsiConsole.MarkupLine($"Ops! This doesn't seem like the a valid PWA... For more info, go to [link]https://www.pwabuilder.com/reportcard?site={uri.ToString().EscapeMarkup()}[/]");
                        _logger.LogWarning(err, "Not a valid PWA ({PWAUrl})", uri);
                        AnsiConsole.MarkupLine($":collision: [bold red]Error while downloading PWA zip file.[/]");
                        return false;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while downloading PWA zip file.");
                        AnsiConsole.MarkupLine($":collision: [bold red]Error while downloading PWA zip file.[/]");
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

        public Task<(int returnCode, DirectoryInfo? outputDirectory)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
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

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? inputDirectory, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
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

            string? appId = app?.Id;
            try
            {
                // Try to find AppId inside the pwaAppInfo.json file
                var pwaAppInfo = await _pwaAppInfoManager.LoadAsync(pathOrUrl, ct);

                appId ??= pwaAppInfo.AppId;
                uri = pwaAppInfo.Uri ?? uri;
                if (appId == null || uri == null)
                {
                    AnsiConsole.MarkupLine($":collision: [bold red]AppId or Uri is not defined.[/]");
                    _logger.LogError("This folder has not been initialized with a PWA. Could not find the AppId or Uri in the pwaAppInfo.json file.");
                    return -1;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($":collision: [bold red]Error while loading pwaAppInfo.json file.[/]");
                _logger.LogError(ex, "Error while loading pwaAppInfo.json file.");
                return -1;
            }

            app = await storePackagedAPI.EnsureAppInitializedAsync(app, () => Task.FromResult<string?>(appId), ct);

            if (app?.Id == null)
            {
                return -1;
            }

            AnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            var output = new DirectoryInfo(pathOrUrl);

            var pwaBuilderExtractedBundle = output.GetDirectories("*_PWABuilderExtractedBundle", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (pwaBuilderExtractedBundle == null)
            {
                return -1;
            }

            var packageFiles = pwaBuilderExtractedBundle?.GetFiles("*.*", SearchOption.AllDirectories)
                                     .Where(f => (f.Extension == ".appxbundle"
                                               || f.Extension == ".msixbundle"
                                               || f.Extension == ".msix"
                                               || f.Extension == ".appx")
                                               && !f.Name.EndsWith(".sideload.msix", StringComparison.OrdinalIgnoreCase));

            if (packageFiles?.Any() != true)
            {
                AnsiConsole.WriteLine("Couldn't find any package to upload.");
                return -1;
            }

            return await storePackagedAPI.PublishAsync(
                app,
                (listingLanguage, ct) => GetFirstSubmissionDataAsync(listingLanguage, uri, ct),
                output,
                packageFiles,
                _browserLauncher,
                _consoleReader,
                _zipFileManager,
                _fileDownloader,
                _azureBlobManager,
                _logger,
                ct);
        }

        private async Task<(string? description, List<SubmissionImage> images)> GetFirstSubmissionDataAsync(string listingLanguage, Uri uri, CancellationToken ct)
        {
            Debug.WriteLine(listingLanguage);

            if (_webManifest == null)
            {
                var webManifestResponse = await AnsiConsole.Status().StartAsync("Fetching WebManifest...", async ctx =>
                {
                    try
                    {
                        return await _pwaBuilderClient.FetchWebManifestAsync(uri, ct);
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while fetching web manifest.");
                        ctx.ErrorStatus("Error while fetching web manifest.");
                        return null;
                    }
                });

                _webManifest = webManifestResponse?.Content?.Json;
            }

            var images = new List<SubmissionImage>();

            if (_webManifest == null)
            {
                return (null, images);
            }

            if (_webManifest.Screenshots?.Any() == true)
            {
                foreach (var screenShot in _webManifest.Screenshots)
                {
                    if (screenShot.Src != null)
                    {
                        images.Add(new SubmissionImage(screenShot.Src, SubmissionImageType.Screenshot));
                    }
                }
            }

            if (_webManifest.Icons?.Any() == true)
            {
                // Order by size
                _webManifest.Icons.Sort((a, b) => a.GetSize().LengthSquared().CompareTo(b.GetSize().LengthSquared()));
                var icon = _webManifest.Icons.Last();

                if (icon.Src != null)
                {
                    images.Add(new SubmissionImage(icon.Src, SubmissionImageType.Icon));
                }
            }

            return (_webManifest.Description, images);
        }
    }
}
