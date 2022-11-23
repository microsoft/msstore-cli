// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

namespace MSStore.CLI.Commands.Init.Setup
{
    internal class PWAProjectConfigurator : IProjectConfigurator, IProjectPackager, IProjectPublisher
    {
        private readonly IConsoleReader _consoleReader;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IPWABuilderClient _pwaBuilderClient;
        private readonly IZipFileManager _zipFileManager;
        private readonly IAzureBlobManager _azureBlobManager;
        private readonly IFileDownloader _fileDownloader;
        private readonly ILogger _logger;

        public PWAProjectConfigurator(
            IConsoleReader consoleReader,
            IBrowserLauncher browserLauncher,
            IPWABuilderClient pwaBuilderClient,
            IZipFileManager zipFileManager,
            IAzureBlobManager azureBlobManager,
            ILogger<PWAProjectConfigurator> logger,
            IFileDownloader fileDownloader)
        {
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _pwaBuilderClient = pwaBuilderClient ?? throw new ArgumentNullException(nameof(pwaBuilderClient));
            _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
            _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
        }

        public string ConfiguratorProjectType => "PWA";

        public bool CanConfigure(string pathOrUrl)
        {
            var uri = GetUri(pathOrUrl);

            return uri != null || ContainsPWAAppInfoJson(pathOrUrl);
        }

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

            AnsiConsole.WriteLine($"You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload it to the Microsoft Store.");
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
                                }
                            },
                            outputZipPath,
                            task,
                            ct);

                        AnsiConsole.MarkupLine($":check_mark_button: [green]PWA Bundle successfully downloaded![/]");
                    }
                    catch (InvalidOperationException err)
                    {
                        AnsiConsole.WriteLine($"Ops! This doesn't seem like the a valid PWA... For more info, go to https://www.pwabuilder.com/testing?site={uri}");
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

            _zipFileManager.ExtractZip(outputZipPath, extractedZipDir);
            var extractedZipDirInfo = new DirectoryInfo(extractedZipDir);

            _logger.LogInformation("Extracted '{ZipPath}' to: '{ExtractedZipDirFullName}'", outputZipPath, extractedZipDirInfo.FullName);

            var appInfoPath = Path.Combine(zipDir, "pwaAppInfo.json");
            using var file = File.Open(appInfoPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(
                file,
                new PWAAppInfo
                {
                    AppId = app.Id,
                    Uri = uri,
                },
                PWAAppInfoSourceGenerationContext.Default.PWAAppInfo,
                ct);

            return (0, output);
        }

        public async Task<bool> ConfigureSubmissionAsync(DevCenterSubmission submission, Uri uri, DevCenterApplication app, CancellationToken ct)
        {
            if (submission.ApplicationPackages == null)
            {
                AnsiConsole.WriteLine("No application packages found.");
                return false;
            }

            var webManifest = await AnsiConsole.Status().StartAsync("Prepating Bundle...", async ctx =>
            {
                try
                {
                    return await _pwaBuilderClient.FetchWebManifestAsync(uri, ct);
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while preparing bundle.");
                    ctx.ErrorStatus("Error while preparing bundle.");
                    return null;
                }
            });

            await FulfillApplicationAsync(app, submission, webManifest?.Content?.Json, ct);

            AnsiConsole.MarkupLine("New Submission [green]properly configured[/].");
            _logger.LogInformation("New Submission properly configured. FileUploadUrl: {FileUploadUrl}", submission.FileUploadUrl);

            return true;
        }

        private async Task FulfillApplicationAsync(DevCenterApplication app, DevCenterSubmission submission, WebManifestJson? webManifest, CancellationToken ct)
        {
            if (submission.ApplicationCategory == DevCenterApplicationCategory.NotSet)
            {
                var categories = Enum.GetNames(typeof(DevCenterApplicationCategory))
                    .Where(c => c != nameof(DevCenterApplicationCategory.NotSet))
                    .ToArray();

                var categoryString = await _consoleReader.SelectionPromptAsync(
                    "Please select the Application Category:",
                    categories,
                    20,
                    ct: ct);

                submission.ApplicationCategory = (DevCenterApplicationCategory)Enum.Parse(typeof(DevCenterApplicationCategory), categoryString);
            }

            if (submission.Listings?.Any() != true)
            {
                submission.Listings = new Dictionary<string, DevCenterListing>();

                AnsiConsole.WriteLine("Lets add listings to your application. Please enter the following information:");

                var listingCount = 0;
                do
                {
                    AnsiConsole.WriteLine("\tHow many listings do you want to add? One is enough, but you might want to support more listing languages.");
                    var listingCountString = await _consoleReader.ReadNextAsync(false, ct);
                    if (!int.TryParse(listingCountString, out listingCount))
                    {
                        AnsiConsole.WriteLine("Invalid listing count.");
                    }
                }
                while (listingCount == 0);

                for (var i = 0; i < listingCount; i++)
                {
                    string? listingLanguage;
                    do
                    {
                        listingLanguage = await _consoleReader.RequestStringAsync("\tEnter the language of the listing (e.g. 'en-US')", false, ct);
                        if (string.IsNullOrEmpty(listingLanguage))
                        {
                            AnsiConsole.WriteLine("Invalid listing language.");
                        }
                    }
                    while (string.IsNullOrEmpty(listingLanguage));

                    var listing = new DevCenterListing
                    {
                        BaseListing = new BaseListing
                        {
                            Title = app.PrimaryName,
                            Description = webManifest?.Description ?? await _consoleReader.RequestStringAsync($"\tEnter the description of the listing ({listingLanguage}):", false, ct),
                        }
                    };

                    if (listing.BaseListing.Images?.Any() != true)
                    {
                        listing.BaseListing.Images = new List<Image>();

                        if (webManifest?.Screenshots?.Any() == true)
                        {
                            foreach (var screenShot in webManifest.Screenshots)
                            {
                                listing.BaseListing.Images.Add(new Image
                                {
                                    FileName = screenShot.Src,
                                    FileStatus = FileStatus.PendingUpload,
                                    ImageType = "Screenshot"
                                });
                            }
                        }

                        if (webManifest?.Icons?.Any() == true)
                        {
                            // Order by size
                            webManifest.Icons.Sort((a, b) => a.GetSize().LengthSquared().CompareTo(b.GetSize().LengthSquared()));
                            var icon = webManifest.Icons.Last();

                            if (icon.Src != null)
                            {
                                listing.BaseListing.Images.Add(new Image
                                {
                                    FileName = icon.Src,
                                    FileStatus = FileStatus.PendingUpload,
                                    ImageType = "Icon"
                                });
                            }
                        }
                    }

                    submission.Listings.Add(listingLanguage, listing);
                }
            }

            if (submission.AllowTargetFutureDeviceFamilies?.Any() != true)
            {
                if (submission.AllowTargetFutureDeviceFamilies == null)
                {
                    submission.AllowTargetFutureDeviceFamilies = new Dictionary<string, bool>();
                }

                submission.AllowTargetFutureDeviceFamilies["Desktop"] = true;
                submission.AllowTargetFutureDeviceFamilies["Mobile"] = false;
                submission.AllowTargetFutureDeviceFamilies["Holographic"] = true;
                submission.AllowTargetFutureDeviceFamilies["Xbox"] = false;
            }
        }

        private async Task CreateImageAsync(string listingKey, Image image, string uploadDir, IProgress<double> progress, CancellationToken ct)
        {
            var fileName = $"{image.ImageType}_{Path.GetFileName(image.FileName)}";

            var imageDirectory = Path.Combine(uploadDir, fileName);
            if (image.FileName != null && await _fileDownloader.DownloadAsync(image.FileName, imageDirectory, progress, ct))
            {
                image.FileName = Path.Combine(listingKey, fileName);
            }
        }

        private async Task<string?> PrepareBundleAsync(DevCenterSubmission submission, DirectoryInfo output, CancellationToken ct)
        {
            if (submission?.ApplicationPackages == null)
            {
                return null;
            }

            AnsiConsole.MarkupLine("Prepating Bundle...");

            return await AnsiConsole.Progress()
                .StartAsync(async ctx =>
            {
                try
                {
                    var packageFiles = output.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                                    .Where(f => (f.Extension == ".appxbundle"
                                             || f.Extension == ".msixbundle"
                                             || f.Extension == ".msix")
                                             && !f.Name.EndsWith(".sideload.msix", StringComparison.OrdinalIgnoreCase));

                    var applicationPackages = submission.ApplicationPackages.FilterUnsupported();

                    var uploadDir = Directory.CreateDirectory(Path.Combine(output.FullName, "Upload"));
                    foreach (var file in packageFiles)
                    {
                        var applicationPackage = applicationPackages.FirstOrDefault(p => Path.GetExtension(p.FileName) == file.Extension);
                        if (applicationPackage != null)
                        {
                            if (applicationPackage.FileStatus == FileStatus.PendingUpload)
                            {
                                submission.ApplicationPackages.Remove(applicationPackage);
                            }
                            else
                            {
                                // Mark as Deleted
                                applicationPackage.FileStatus = FileStatus.PendingDelete;
                            }
                        }

                        var newApplicationPackage = new ApplicationPackage
                        {
                            FileStatus = FileStatus.PendingUpload,
                            FileName = file.Name
                        };

                        submission.ApplicationPackages.Add(newApplicationPackage);

                        _logger.LogInformation("Moving '{FileFullName}' to zip bundle folder.", file.FullName);
                        File.Move(file.FullName, Path.Combine(uploadDir.FullName, file.Name));
                    }

                    // Add images to Bundle
                    if (submission.Listings != null)
                    {
                        var tasks = new List<Task>();
                        foreach (var listing in submission.Listings)
                        {
                            if (listing.Value?.BaseListing?.Images?.Any() == true)
                            {
                                var imagesToDownload = listing.Value.BaseListing.Images.Where(i =>
                                        i.FileStatus == FileStatus.PendingUpload &&
                                        i.FileName != null &&
                                        i.FileName.StartsWith("http", StringComparison.OrdinalIgnoreCase));

                                if (imagesToDownload.Any())
                                {
                                    var listingUploadDir = Path.Combine(uploadDir.FullName, listing.Key);

                                    Directory.CreateDirectory(listingUploadDir);

                                    foreach (var image in imagesToDownload)
                                    {
                                        var task = ctx.AddTask($"[green]Downloading Image '{image.FileName}'[/]");
                                        tasks.Add(CreateImageAsync(listing.Key, image, listingUploadDir, task, ct));
                                    }
                                }
                            }
                        }

                        await Task.WhenAll(tasks);
                    }

                    var uploadZipFilePath = Path.Combine(output.FullName, "Upload.zip");

                    _zipFileManager.CreateFromDirectory(uploadDir.FullName, uploadZipFilePath);

                    AnsiConsole.MarkupLine(":check_mark_button: [green]Zip Bundle is configured and ready to be uploaded![/]");

                    return uploadZipFilePath;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while preparing bundle.");
                    AnsiConsole.MarkupLine($":collision: [bold red]Error while preparing bundle.[/]");
                    return null;
                }
            });
        }

        public Task<(int returnCode, FileInfo? outputFile)> PackageAsync(string pathOrUrl, DevCenterApplication? app, DirectoryInfo? output, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            if (GetUri(pathOrUrl) != null && output != null)
            {
                pathOrUrl = output.FullName;
            }

            if (pathOrUrl == null)
            {
                return Task.FromResult<(int, FileInfo?)>((-1, null));
            }

            return Task.FromResult((0, (FileInfo?)new FileInfo(fileName: pathOrUrl)));
        }

        public async Task<int> PublishAsync(string pathOrUrl, DevCenterApplication? app, FileInfo? input, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            bool success = true;

            if (GetUri(pathOrUrl) != null && input != null)
            {
                pathOrUrl = input.FullName;
            }

            if (pathOrUrl == null)
            {
                return -1;
            }

            string? appId = app?.Id;
            Uri uri;
            try
            {
                var appInfoPath = Path.Combine(pathOrUrl, "pwaAppInfo.json");
                using var file = File.Open(appInfoPath, FileMode.Open);

                // Try to find AppId inside the pwaAppInfo.json file
                var pwaAppInfo = await JsonSerializer.DeserializeAsync(file, PWAAppInfoSourceGenerationContext.Default.PWAAppInfo, ct);

                appId ??= pwaAppInfo?.AppId;
                uri = pwaAppInfo?.Uri ?? throw new Exception("Uri is null");
                if (appId == null)
                {
                    throw new MSStoreException("Failed to find the AppId in the pubspec.yaml file.");
                }
            }
            catch (Exception)
            {
                return -1;
            }

            if (app?.Id == null)
            {
                try
                {
                    success = await AnsiConsole.Status().StartAsync("Retrieving application...", async ctx =>
                    {
                        try
                        {
                            app = await storePackagedAPI.GetApplicationAsync(appId, ct);

                            ctx.SuccessStatus("Ok! Found the app!");
                        }
                        catch (Exception)
                        {
                            ctx.ErrorStatus("Could not retrieve your application. Please make sure you have the correct AppId.");

                            return false;
                        }

                        return true;
                    });

                    if (!success || app?.Id == null)
                    {
                        return -1;
                    }
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            AnsiConsole.MarkupLine($"AppId: [green bold]{app.Id}[/]");

            var pendingSubmissionId = app.PendingApplicationSubmission?.Id;

            // Do not delete if first submission
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission != null)
            {
                success = await storePackagedAPI.DeleteSubmissionAsync(app.Id, pendingSubmissionId, _browserLauncher, _logger, ct);

                if (!success)
                {
                    return -1;
                }
            }

            DevCenterSubmission? submission = null;

            // If first submission, just use it // TODO, check that can update
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission == null)
            {
                submission = await storePackagedAPI.GetExistingSubmission(app.Id, pendingSubmissionId, _logger, ct);

                if (submission == null || submission.Id == null)
                {
                    _logger.LogError("Could not create or retrieve submission. Please try again.");
                    AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                    return -1;
                }

                if (submission.FileUploadUrl == null)
                {
                    _logger.LogError("Retrieved a submission that was created in Partner Center. Please, delete it and try again.");
                    AnsiConsole.WriteLine("Retrieved a submission that was created in Partner Center. Please, delete it and try again.");
                    return -1;
                }

                var qs = System.Web.HttpUtility.ParseQueryString(submission.FileUploadUrl);
                if (!DateTime.TryParse(qs["se"], out var fileUploadExpire) || fileUploadExpire < DateTime.UtcNow)
                {
                    success = await storePackagedAPI.DeleteSubmissionAsync(app.Id, submission.Id, _browserLauncher, _logger, ct);

                    if (!success)
                    {
                        return -1;
                    }

                    submission = null;
                }
            }

            if (submission == null)
            {
                var newSubmission = await storePackagedAPI.CreateNewSubmissionAsync(app.Id, _logger, ct);
                if (newSubmission != null)
                {
                    submission = newSubmission;
                }

                success = submission != null;
            }

            if (!success || submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                _logger.LogError("Could not create or retrieve submission. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                return -1;
            }

            submission = await storePackagedAPI.GetExistingSubmission(app.Id, submission.Id, _logger, ct);

            if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                _logger.LogError("Could not retrieve submission. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                return -1;
            }

            if (submission.ApplicationPackages == null)
            {
                AnsiConsole.WriteLine("No application packages found.");
                return -1;
            }

            if (await ConfigureSubmissionAsync(submission, uri, app, ct) == false)
            {
                return -1;
            }

            var uploadZipFilePath = await PrepareBundleAsync(submission, new DirectoryInfo(pathOrUrl), ct);

            if (uploadZipFilePath == null)
            {
                return -1;
            }

            submission = await storePackagedAPI.UpdateSubmissionAsync(app.Id, submission.Id, submission, ct);

            if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                _logger.LogError("Could not retrieve FileUploadUrl. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve FileUploadUrl. Please try again.");
                return -1;
            }

            success = await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Uploading Bundle to [u]Azure blob[/][/]");
                    try
                    {
                        await _azureBlobManager.UploadFileAsync(submission.FileUploadUrl, uploadZipFilePath, task, ct);
                        AnsiConsole.MarkupLine($":check_mark_button: [green]Successfully uploaded the application package.[/]");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while uploading the application package.");
                        AnsiConsole.WriteLine("Error while uploading the application package.");
                        return false;
                    }
                });

            if (!success)
            {
                return -1;
            }

            var submissionCommit = await storePackagedAPI.CommitSubmissionAsync(app.Id, submission.Id, ct);

            AnsiConsole.WriteLine("Waiting for the submission commit processing to complete. This may take a couple of minutes.");
            AnsiConsole.MarkupLine($"Submission Committed - Status=[green u]{submissionCommit.Status}[/]");

            var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(app.Id, submission.Id, true, _logger, ct: ct);

            if (lastSubmissionStatus == null)
            {
                return -1;
            }

            return await storePackagedAPI.HandleLastSubmissionStatusAsync(lastSubmissionStatus, app.Id, submission.Id, _consoleReader, _browserLauncher, _logger, ct);
        }
    }
}
