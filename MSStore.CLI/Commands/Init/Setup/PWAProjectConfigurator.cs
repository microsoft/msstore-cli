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
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.PWABuilder;
using Spectre.Console;

namespace MSStore.CLI.Commands.Init.Setup
{
    internal class PWAProjectConfigurator : IProjectConfigurator
    {
        private readonly IConsoleReader _consoleReader;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IPWABuilderClient _pwaBuilderClient;
        private readonly IZipFileManager _zipFileManager;
        private readonly IAzureBlobManager _azureBlobManager;
        private readonly ILogger _logger;

        public PWAProjectConfigurator(
            IConsoleReader consoleReader,
            IBrowserLauncher browserLauncher,
            IPWABuilderClient pwaBuilderClient,
            IZipFileManager zipFileManager,
            IAzureBlobManager azureBlobManager,
            ILogger<PWAProjectConfigurator> logger)
        {
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _pwaBuilderClient = pwaBuilderClient ?? throw new ArgumentNullException(nameof(pwaBuilderClient));
            _zipFileManager = zipFileManager ?? throw new ArgumentNullException(nameof(zipFileManager));
            _azureBlobManager = azureBlobManager ?? throw new ArgumentNullException(nameof(azureBlobManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ConfiguratorProjectType => "PWA";

        internal static TimeSpan DefaultSubmissionPollDelay { get; set; } = TimeSpan.FromSeconds(30);

        public bool CanConfigure(string pathOrUrl)
        {
            var uri = GetUri(pathOrUrl);

            return uri != null;
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

        public async Task<int> ConfigureAsync(string pathOrUrl, AccountEnrollment account, DevCenterApplication app, IStorePackagedAPI storePackagedAPI, CancellationToken ct)
        {
            var uri = GetUri(pathOrUrl);

            if (uri == null)
            {
                return -1;
            }

            if (string.IsNullOrEmpty(app.Id))
            {
                return -1;
            }

            var pendingSubmissionId = app.PendingApplicationSubmission?.Id;

            bool success = true;

            // Do not delete if first submission
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission != null)
            {
                success = await DeleteSubmissionAsync(storePackagedAPI, app.Id, pendingSubmissionId, ct);

                if (!success)
                {
                    return -1;
                }
            }

            DevCenterSubmission? submission = null;

            // If first submission, just use it // TODO, check that can update
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission == null)
            {
                submission = await GetExistingSubmission(storePackagedAPI, app.Id, pendingSubmissionId, ct);

                if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
                {
                    _logger.LogError("Could not create or retrieve submission. Please try again.");
                    return -1;
                }

                var qs = System.Web.HttpUtility.ParseQueryString(submission.FileUploadUrl);
                if (!DateTime.TryParse(qs["se"], out var fileUploadExpire) || fileUploadExpire < DateTime.UtcNow)
                {
                    success = await DeleteSubmissionAsync(storePackagedAPI, app.Id, submission.Id, ct);

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
                return -1;
            }

            submission = await GetExistingSubmission(storePackagedAPI, app.Id, submission.Id, ct);

            if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                _logger.LogError("Could not retrieve submission. Please try again.");
                return -1;
            }

            if (submission.ApplicationPackages == null)
            {
                AnsiConsole.WriteLine("No application packages found.");
                return -1;
            }

            var uploadZipFilePath = await ConfigureSubmissionAsync(uri, account, app, submission, ct);

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

            // Let's periodically check the status until it changes from "CommitsStarted" to either
            // successful status or a failure.
            DevCenterSubmissionStatusResponse? submissionStatus = null;
            do
            {
                await Task.Delay(DefaultSubmissionPollDelay, ct);
                submissionStatus = await storePackagedAPI.GetSubmissionStatusAsync(app.Id, submission.Id, ct);

                AnsiConsole.MarkupLine($"Submission Status - [green]{submissionStatus.Status}[/]");
                if (submissionStatus.StatusDetails?.Errors?.Any() == true)
                {
                    var table = new Table
                    {
                        Title = new TableTitle($":red_exclamation_mark: [b]Submission Errors[/]")
                    };
                    table.AddColumns("Code", "Details");
                    foreach (var error in submissionStatus.StatusDetails.Errors)
                    {
                        table.AddRow($"[bold u]{error.Code}[/]", error.Details!);
                    }

                    AnsiConsole.Write(table);
                }

                if (submissionStatus.StatusDetails?.Warnings?.Any() == true)
                {
                    var onlyLogCodes = new List<string>()
                        {
                            "SalesUnsupportedWarning"
                        };

                    var filteredOut = submissionStatus.StatusDetails.Warnings.Where(w => !onlyLogCodes.Contains(w.Code!));
                    if (filteredOut.Any())
                    {
                        var table = new Table
                        {
                            Title = new TableTitle($":warning: [b]Submission Warnings[/]")
                        };
                        table.AddColumns("Code", "Details");
                        foreach (var warning in filteredOut)
                        {
                            table.AddRow($"[bold u]{warning.Code}[/]", warning.Details!);
                        }

                        AnsiConsole.Write(table);
                    }

                    foreach (var error in submissionStatus.StatusDetails.Warnings.Where(w => onlyLogCodes.Contains(w.Code!)))
                    {
                        _logger.LogInformation("{Code} - {Details}", error.Code, error.Details);
                    }
                }

                if (submissionStatus.StatusDetails?.CertificationReports?.Any() == true)
                {
                    var table = new Table
                    {
                        Title = new TableTitle($":paperclip: [b]Certification Reports[/]")
                    };
                    table.AddColumns("Date", "Report Url");
                    foreach (var warning in submissionStatus.StatusDetails.CertificationReports)
                    {
                        table.AddRow($"[bold u]{warning.Date}[/]", $"[bold u]{warning.ReportUrl}[/]");
                    }

                    AnsiConsole.Write(table);
                }

                AnsiConsole.WriteLine();
            }
            while ("CommitStarted".Equals(submissionStatus.Status, StringComparison.Ordinal)
                    && submissionStatus.StatusDetails?.Errors?.Any() == false
                    && submissionStatus.StatusDetails?.CertificationReports?.Any() == false);

            if ("CommitFailed".Equals(submissionStatus.Status, StringComparison.Ordinal))
            {
                AnsiConsole.WriteLine("Submission has failed. Please checkt the Errors collection of the submissionResource response.");
                return -1;
            }
            else
            {
                AnsiConsole.WriteLine("Submission commit success! Here are some data:");
                submission = await GetExistingSubmission(storePackagedAPI, app.Id, submission.Id, ct);

                if (submission == null || submission.Id == null)
                {
                    _logger.LogError("Could not retrieve submission. Please try again.");
                    return -1;
                }

                AnsiConsole.WriteLine("Packages: " + submission.ApplicationPackages);
            }

            return 0;
        }

        public async Task<string?> ConfigureSubmissionAsync(Uri uri, AccountEnrollment account, DevCenterApplication app, DevCenterSubmission submission, CancellationToken ct)
        {
            if (submission.ApplicationPackages == null)
            {
                AnsiConsole.WriteLine("No application packages found.");
                return null;
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

            var maxVersion = new Version();
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

            if (maxVersion.ToString() == "0.0")
            {
                maxVersion = new Version(1, 0, 0);
            }

            AnsiConsole.MarkupLine("New Submission [green]properly configured[/].");
            _logger.LogInformation("New Submission properly configured. FileUploadUrl: {FileUploadUrl}", submission.FileUploadUrl);

            AnsiConsole.WriteLine();

            AnsiConsole.WriteLine($"You've provided a URL, so we'll use PWABuilder.com to setup your PWA and upload it to the Microsoft Store.");
            AnsiConsole.WriteLine();

            var zipPath = string.Empty;
            bool success = await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Downloading PWA Bundle From [u]PWABuilder.com[/][/]");

                    try
                    {
                        var classicVersion = new Version(maxVersion.Major, maxVersion.Minor, maxVersion.Build + 1);
                        var version = new Version(maxVersion.Major, maxVersion.Minor, maxVersion.Build + 2);
                        zipPath = await _pwaBuilderClient.GenerateZipAsync(
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
                                    DisplayName = account.Name,
                                    CommonName = app.PublisherName
                                }
                            },
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

            if (!success)
            {
                return null;
            }

            return PrepareBundle(submission, zipPath);
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
                            listing.BaseListing.Images.Add(new Image
                            {
                                // TODO: Add file to Bundle and use relative path, not actual Web Src
                                FileName = icon.Src,
                                FileStatus = FileStatus.PendingUpload,
                                ImageType = "Icon"
                            });
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

            // InvalidMicrosoftAgeRating
        }

        private string? PrepareBundle(DevCenterSubmission submission, string zipPath)
        {
            if (submission?.ApplicationPackages == null)
            {
                return null;
            }

            return AnsiConsole.Status().Start("Prepating Bundle...", ctx =>
            {
                try
                {
                    var zipDir = Path.GetDirectoryName(zipPath) ?? Path.GetTempPath();

                    var extractedZipDir = Path.Combine(zipDir, Path.GetFileNameWithoutExtension(zipPath));

                    _zipFileManager.ExtractZip(zipPath, extractedZipDir);
                    var extractedZipDirInfo = new DirectoryInfo(extractedZipDir);

                    _logger.LogInformation("Extracted '{ZipPath}' to: '{ExtractedZipDirFullName}'", zipPath, extractedZipDirInfo.FullName);

                    var uploadDir = Directory.CreateDirectory(Path.Combine(extractedZipDirInfo.FullName, "Upload"));
                    var packageFiles = extractedZipDirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                                    .Where(f => (f.Extension == ".appxbundle"
                                             || f.Extension == ".msixbundle"
                                             || f.Extension == ".msix")
                                             && !f.Name.EndsWith(".sideload.msix", StringComparison.OrdinalIgnoreCase));

                    var applicationPackages = submission.ApplicationPackages.FilterUnsupported();

                    foreach (var file in packageFiles)
                    {
                        var applicationPackage = applicationPackages.FirstOrDefault(p => Path.GetExtension(p.FileName) == file.Extension);
                        if (applicationPackage != null)
                        {
                            if (applicationPackage.FileStatus == "PendingUpload")
                            {
                                submission.ApplicationPackages.Remove(applicationPackage);
                            }
                            else
                            {
                                // Mark as Deleted
                                applicationPackage.FileStatus = "PendingDelete";
                            }
                        }

                        var newApplicationPackage = new ApplicationPackage
                        {
                            FileStatus = "PendingUpload",
                            FileName = file.Name
                        };

                        submission.ApplicationPackages.Add(newApplicationPackage);

                        _logger.LogInformation("Moving '{FileFullName}' to zip bundle folder.", file.FullName);
                        File.Move(file.FullName, Path.Combine(uploadDir.FullName, file.Name));
                    }

                    var uploadZipFilePath = Path.Combine(extractedZipDirInfo.FullName, "Upload.zip");

                    _zipFileManager.CreateFromDirectory(uploadDir.FullName, uploadZipFilePath);

                    AnsiConsole.MarkupLine(":check_mark_button: [green]Zip Bundle is configured and ready to be uploaded![/]");

                    return uploadZipFilePath;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while preparing bundle.");
                    ctx.ErrorStatus("Error while preparing bundle.");
                    return null;
                }
            });
        }

        private async Task<DevCenterSubmission?> GetExistingSubmission(IStorePackagedAPI storePackagedAPI, string appId, string submissionId, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Retrieving existing Submission", async ctx =>
            {
                try
                {
                    var submission = await storePackagedAPI.GetSubmissionAsync(appId, submissionId, ct);

                    AnsiConsole.MarkupLine($":check_mark_button: [green]Submission retrieved[/]");
                    _logger.LogInformation("Submission retrieved. Id = '{SubmissionId}'", submission.Id);

                    return submission;
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while retrieving submission.");
                    ctx.ErrorStatus("Error while retrieving submission. Please try again.");
                    return null;
                }
            });
        }

        private async Task<bool> DeleteSubmissionAsync(IStorePackagedAPI storePackagedAPI, string appId, string pendingSubmissionId, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Deleting existing Submission", async ctx =>
            {
                try
                {
                    var devCenterError = await storePackagedAPI.DeleteSubmissionAsync(appId, pendingSubmissionId, ct);
                    if (devCenterError != null)
                    {
                        AnsiConsole.WriteLine(devCenterError.Message ?? string.Empty);
                        if (devCenterError.Code == "InvalidOperation" &&
                            devCenterError.Source == "Ingestion Api" &&
                            devCenterError.Target == "applicationSubmission")
                        {
                            var existingSubmission = await storePackagedAPI.GetSubmissionAsync(appId, pendingSubmissionId, ct);
                            AnsiConsole.WriteLine(existingSubmission.Id ?? string.Empty);

                            _browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/products/{appId}/submissions/{existingSubmission.Id}");
                            return false;
                        }
                    }

                    ctx.SuccessStatus("Existing submission deleted!");
                }
                catch (Exception err)
                {
                    _logger.LogError(err, "Error while deleting existing submission.");
                    ctx.ErrorStatus("Error while deleting existing submission. Please try again.");
                    return false;
                }

                return true;
            });
        }
    }
}
