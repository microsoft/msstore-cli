// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class IStorePackagedAPIExtensions
    {
        internal delegate Task<(string? Description, List<SubmissionImage> Images)> FirstSubmissionDataCallback(string listingLanguage, CancellationToken ct);

        public static async Task<DevCenterSubmission?> GetAnySubmissionAsync(this IStorePackagedAPI storePackagedAPI, DevCenterApplication application, StatusContext ctx, ILogger logger, CancellationToken ct)
        {
            if (application.Id == null)
            {
                return null;
            }

            DevCenterSubmission? submission = null;
            if (application.PendingApplicationSubmission?.Id != null)
            {
                AnsiConsole.MarkupLine($"Found [green]Pending Submission[/].");
                logger.LogInformation("Found Pending Submission with Id '{ApplicationPendingApplicationSubmissionId}'", application.PendingApplicationSubmission.Id);
                ctx.Status("Retrieving Pending Submission");
                submission = await storePackagedAPI.GetSubmissionAsync(application.Id, application.PendingApplicationSubmission.Id, ct);
            }
            else if (application.LastPublishedApplicationSubmission?.Id != null)
            {
                AnsiConsole.MarkupLine("Could not find a Pending Submission, but found the [green]Last Published Submission[/].");
                logger.LogInformation("Could not find a Pending Submission, but found the Last Published Submission with Id '{ApplicationLastPublishedApplicationSubmissionId}'", application.LastPublishedApplicationSubmission.Id);
                ctx.Status("Retrieving Last Published Submission");
                submission = await storePackagedAPI.GetSubmissionAsync(application.Id, application.LastPublishedApplicationSubmission.Id, ct);
            }

            return submission;
        }

        public static async Task<DevCenterSubmission?> CreateNewSubmissionAsync(this IStorePackagedAPI storePackagedAPI, string productId, ILogger logger, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Creating new Submission", async ctx =>
            {
                try
                {
                    var submission = await storePackagedAPI.CreateSubmissionAsync(productId, ct);

                    ctx.SuccessStatus($"Submission created.");
                    logger.LogInformation("Submission created. Id={SubmissionId}", submission.Id);

                    return submission;
                }
                catch (Exception err)
                {
                    logger.LogError(err, "Error while creating submission.");
                    ctx.ErrorStatus("Error while creating submission. Please try again.");
                    return null;
                }
            });
        }

        public static async Task<DevCenterSubmission?> GetExistingSubmission(this IStorePackagedAPI storePackagedAPI, string appId, string submissionId, ILogger logger, CancellationToken ct)
        {
            return await AnsiConsole.Status().StartAsync("Retrieving existing Submission", async ctx =>
            {
                try
                {
                    var submission = await storePackagedAPI.GetSubmissionAsync(appId, submissionId, ct);

                    AnsiConsole.MarkupLine($":check_mark_button: [green]Submission retrieved[/]");
                    logger.LogInformation("Submission retrieved. Id = '{SubmissionId}'", submission.Id);

                    return submission;
                }
                catch (Exception err)
                {
                    logger.LogError(err, "Error while retrieving submission.");
                    ctx.ErrorStatus("Error while retrieving submission. Please try again.");
                    return null;
                }
            });
        }

        private static async IAsyncEnumerable<DevCenterSubmissionStatusResponse> EnumerateSubmissionStatusAsync(this IStorePackagedAPI storePackagedAPI, string productId, string submissionId, bool waitFirst, [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Let's periodically check the status until it changes from "CommitsStarted" to either
            // successful status or a failure.
            DevCenterSubmissionStatusResponse? submissionStatus;
            do
            {
                if (waitFirst)
                {
                    await Task.Delay(StorePackagedAPI.DefaultSubmissionPollDelay, ct);
                }

                waitFirst = true;
                submissionStatus = await storePackagedAPI.GetSubmissionStatusAsync(productId, submissionId, ct);

                yield return submissionStatus;
            }
            while ("CommitStarted".Equals(submissionStatus.Status, StringComparison.Ordinal)
                    && submissionStatus.StatusDetails?.Errors?.Any() != true
                    && submissionStatus.StatusDetails?.CertificationReports?.Any() != true);
        }

        public static async Task<DevCenterSubmissionStatusResponse?> PollSubmissionStatusAsync(this IStorePackagedAPI storePackagedAPI, string productId, string submissionId, bool waitFirst, ILogger? logger, CancellationToken ct = default)
        {
            DevCenterSubmissionStatusResponse? lastSubmissionStatus = null;
            await foreach (var submissionStatus in storePackagedAPI.EnumerateSubmissionStatusAsync(productId, submissionId, waitFirst, ct: ct))
            {
                AnsiConsole.MarkupLine($"Submission Status - [green]{submissionStatus.Status}[/]");
                submissionStatus.StatusDetails?.PrintAllTables(productId, submissionId, logger);

                AnsiConsole.WriteLine();

                lastSubmissionStatus = submissionStatus;
            }

            return lastSubmissionStatus;
        }

        public static async Task<int> HandleLastSubmissionStatusAsync(this IStorePackagedAPI storePackagedAPI, DevCenterSubmissionStatusResponse lastSubmissionStatus, string productId, string submissionId, IBrowserLauncher browserLauncher, ILogger logger, CancellationToken ct = default)
        {
            if ("CommitFailed".Equals(lastSubmissionStatus?.Status, StringComparison.Ordinal))
            {
                var error = lastSubmissionStatus.StatusDetails?.Errors?.FirstOrDefault();
                if (lastSubmissionStatus.StatusDetails?.Errors?.Count == 1 &&
                    error?.Code == "InvalidMicrosoftAgeRating")
                {
                    AnsiConsole.WriteLine("Submission has failed. For the first submission of a new application, you need to complete the Microsoft Age Rating at the Microsoft Partner Center.");

                    await browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}/ageratings", true, ct);
                }
                else if (lastSubmissionStatus.StatusDetails?.Errors?.Count == 1 &&
                    error?.Code == "InvalidState")
                {
                    AnsiConsole.WriteLine("Submission has failed. Submission has active validation errors which cannot be exposed via API.");

                    await browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}", true, ct);
                }
                else
                {
                    AnsiConsole.WriteLine("Submission has failed. Please check the Errors collection of the submissionResource response.");

                    await browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}", true, ct);
                }

                return -1;
            }
            else
            {
                var submission = await storePackagedAPI.GetExistingSubmission(productId, submissionId, logger, ct);

                if (submission == null || submission.Id == null)
                {
                    logger.LogError("Could not retrieve submission. Please try again.");
                    return -1;
                }

                if (submission.ApplicationPackages != null)
                {
                    AnsiConsole.WriteLine("Submission commit success! Here is some data:");
                    AnsiConsole.WriteLine("Packages:");
                    foreach (var applicationPackage in submission.ApplicationPackages)
                    {
                        AnsiConsole.WriteLine(applicationPackage.FileName ?? string.Empty);
                    }
                }
            }

            return 0;
        }

        public static async Task<bool> DeleteSubmissionAsync(this IStorePackagedAPI storePackagedAPI, string appId, string pendingSubmissionId, IBrowserLauncher browserLauncher, ILogger logger, CancellationToken ct)
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
                            await browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/products/{appId}/submissions/{existingSubmission.Id}", true, ct);
                            return false;
                        }
                    }

                    ctx.SuccessStatus("Existing submission deleted!");
                }
                catch (MSStoreHttpException err)
                {
                    if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        logger.LogError(err, "Could not delete the submission.");
                        ctx.ErrorStatus("Could not delete the submission.");
                    }
                    else
                    {
                        logger.LogError(err, "Error while deleting application's submission.");
                        ctx.ErrorStatus("Error while deleting submission.");
                    }

                    return false;
                }
                catch (Exception err)
                {
                    logger.LogError(err, "Error while deleting submission.");
                    ctx.ErrorStatus("Error while deleting submission. Please try again.");
                    return false;
                }

                return true;
            });
        }

        public static async Task<DevCenterApplication?> EnsureAppInitializedAsync(this IStorePackagedAPI storePackagedAPI, DevCenterApplication? application, FileInfo? directoryInfo, IProjectPublisher projectPublisher, CancellationToken ct)
        {
            if (application?.Id == null)
            {
                var appId = await projectPublisher.GetAppIdAsync(directoryInfo, ct);
                if (appId == null)
                {
                    throw new MSStoreException("Failed to find the AppId.");
                }

                await AnsiConsole.Status().StartAsync("Retrieving application...", async ctx =>
                {
                    try
                    {
                        application = await storePackagedAPI.GetApplicationAsync(appId, ct);

                        ctx.SuccessStatus("Ok! Found the app!");
                    }
                    catch (Exception)
                    {
                        ctx.ErrorStatus("Could not retrieve your application. Please make sure you have the correct AppId.");
                    }

                    return true;
                });
            }

            return application;
        }

        public static async Task<int> PublishAsync(this IStorePackagedAPI storePackagedAPI, DevCenterApplication app, FirstSubmissionDataCallback firstSubmissionDataCallback, DirectoryInfo output, IEnumerable<FileInfo> input, IBrowserLauncher _browserLauncher, IConsoleReader consoleReader, IZipFileManager zipFileManager, IFileDownloader fileDownloader, IAzureBlobManager azureBlobManager, ILogger logger, CancellationToken ct)
        {
            if (app?.Id == null)
            {
                return -1;
            }

            var pendingSubmissionId = app.PendingApplicationSubmission?.Id;
            bool success = true;

            // Do not delete if first submission
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission != null)
            {
                success = await storePackagedAPI.DeleteSubmissionAsync(app.Id, pendingSubmissionId, _browserLauncher, logger, ct);

                if (!success)
                {
                    return -1;
                }
            }

            DevCenterSubmission? submission = null;

            // If first submission, just use it // TODO, check that can update
            if (pendingSubmissionId != null && app.LastPublishedApplicationSubmission == null)
            {
                submission = await storePackagedAPI.GetExistingSubmission(app.Id, pendingSubmissionId, logger, ct);

                if (submission == null || submission.Id == null)
                {
                    logger.LogError("Could not create or retrieve submission. Please try again.");
                    AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                    return -1;
                }

                if (submission.FileUploadUrl == null)
                {
                    const string message = "Retrieved a submission that was created in Partner Center. We can't upload the packages for submissions created in Partner Center. Please, delete it and try again.";
                    logger.LogError(message);
                    AnsiConsole.WriteLine(message);
                    return -1;
                }

                var qs = System.Web.HttpUtility.ParseQueryString(submission.FileUploadUrl);
                if (!DateTime.TryParse(qs["se"], out var fileUploadExpire) || fileUploadExpire < DateTime.UtcNow)
                {
                    success = await storePackagedAPI.DeleteSubmissionAsync(app.Id, submission.Id, _browserLauncher, logger, ct);

                    if (!success)
                    {
                        return -1;
                    }

                    submission = null;
                }
            }

            if (submission == null)
            {
                var newSubmission = await storePackagedAPI.CreateNewSubmissionAsync(app.Id, logger, ct);
                if (newSubmission != null)
                {
                    submission = newSubmission;
                }

                success = submission != null;
            }

            if (!success || submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                logger.LogError("Could not create or retrieve submission. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                return -1;
            }

            submission = await storePackagedAPI.GetExistingSubmission(app.Id, submission.Id, logger, ct);

            if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                logger.LogError("Could not retrieve submission. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve submission. Please try again.");
                return -1;
            }

            if (submission.ApplicationPackages == null)
            {
                AnsiConsole.WriteLine("No application packages found.");
                return -1;
            }

            if (submission?.Id == null)
            {
                return -1;
            }

            await FulfillApplicationAsync(app, submission, firstSubmissionDataCallback, consoleReader, ct);

            AnsiConsole.MarkupLine("New Submission [green]properly configured[/].");
            logger.LogInformation("New Submission properly configured. FileUploadUrl: {FileUploadUrl}", submission.FileUploadUrl);

            var uploadZipFilePath = await PrepareBundleAsync(submission, output, input, zipFileManager, fileDownloader, logger, ct);

            if (uploadZipFilePath == null)
            {
                return -1;
            }

            submission = await storePackagedAPI.UpdateSubmissionAsync(app.Id, submission.Id, submission, ct);

            if (submission == null || submission.Id == null || submission.FileUploadUrl == null)
            {
                logger.LogError("Could not retrieve FileUploadUrl. Please try again.");
                AnsiConsole.WriteLine("Could not retrieve FileUploadUrl. Please try again.");
                return -1;
            }

            success = await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Uploading Bundle to [u]Azure blob[/][/]");
                    try
                    {
                        await azureBlobManager.UploadFileAsync(submission.FileUploadUrl, uploadZipFilePath, task, ct);
                        AnsiConsole.MarkupLine($":check_mark_button: [green]Successfully uploaded the application package.[/]");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while uploading the application package.");
                        AnsiConsole.WriteLine("Error while uploading the application package.");
                        return false;
                    }
                });

            if (!success)
            {
                return -1;
            }

            var submissionCommit = await storePackagedAPI.CommitSubmissionAsync(app.Id, submission.Id, ct);

            if (submissionCommit == null)
            {
                AnsiConsole.MarkupLine(":collision: [bold red]Could not commit submission.[/]");
                return -1;
            }

            if (submissionCommit.Status == null)
            {
                AnsiConsole.MarkupLine(":collision: [bold red]Could not retrieve submission status.[/]");
                AnsiConsole.MarkupLine($"[red]{submissionCommit.ToErrorMessage()}[/]");

                return -2;
            }

            AnsiConsole.WriteLine("Waiting for the submission commit processing to complete. This may take a couple of minutes.");
            AnsiConsole.MarkupLine($"Submission Committed - Status=[green u]{submissionCommit.Status}[/]");

            var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(app.Id, submission.Id, true, logger, ct: ct);

            if (lastSubmissionStatus == null)
            {
                return -1;
            }

            return await storePackagedAPI.HandleLastSubmissionStatusAsync(lastSubmissionStatus, app.Id, submission.Id, _browserLauncher, logger, ct);
        }

        private static async Task<string?> PrepareBundleAsync(DevCenterSubmission submission, DirectoryInfo output, IEnumerable<FileInfo> packageFiles, IZipFileManager zipFileManager, IFileDownloader fileDownloader, ILogger logger, CancellationToken ct)
        {
            if (submission?.ApplicationPackages == null)
            {
                return null;
            }

            AnsiConsole.MarkupLine("Prepating Bundle...");

            return await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    DirectoryInfo? uploadDir = null;
                    try
                    {
                        var applicationPackages = submission.ApplicationPackages.FilterUnsupported();

                        uploadDir = Directory.CreateDirectory(Path.Combine(output.FullName, $"Upload_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}"));

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

                            logger.LogInformation("Copying '{FileFullName}' to zip bundle folder.", file.FullName);
                            File.Copy(file.FullName, Path.Combine(uploadDir.FullName, file.Name));
                        }

                        // Add images to Bundle
                        if (submission.Listings != null)
                        {
                            var tasks = new List<Task<bool>>();
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
                                            tasks.Add(CreateImageAsync(listing.Key, image, listingUploadDir, task, fileDownloader, logger, ct));
                                        }
                                    }
                                }
                            }

                            await Task.WhenAll(tasks);

                            if (tasks.Any(t => !t.Result))
                            {
                                AnsiConsole.MarkupLine("Error while downloading images. Please try again.");
                                return null;
                            }
                        }

                        var uploadZipFilePath = Path.Combine(output.FullName, "Upload.zip");

                        zipFileManager.CreateFromDirectory(uploadDir.FullName, uploadZipFilePath);

                        AnsiConsole.MarkupLine(":check_mark_button: [green]Zip Bundle is configured and ready to be uploaded![/]");

                        return uploadZipFilePath;
                    }
                    catch (Exception err)
                    {
                        logger.LogError(err, "Error while preparing bundle.");
                        AnsiConsole.MarkupLine($":collision: [bold red]Error while preparing bundle.[/]");
                        return null;
                    }
                    finally
                    {
                        uploadDir?.Delete(true);
                    }
                });
        }

        private static async Task<bool> CreateImageAsync(string listingKey, Image image, string uploadDir, IProgress<double> progress, IFileDownloader fileDownloader, ILogger logger, CancellationToken ct)
        {
            var fileName = $"{image.ImageType}_{Path.GetFileName(image.FileName)}";

            var destinationFileName = Path.Combine(uploadDir, fileName);
            if (image.FileName == null)
            {
                return false;
            }

            var result = await fileDownloader.DownloadAsync(image.FileName, destinationFileName, progress, logger, ct);

            if (result)
            {
                image.FileName = Path.Combine(listingKey, fileName);
                return true;
            }

            return false;
        }

        private static async Task FulfillApplicationAsync(DevCenterApplication app, DevCenterSubmission submission, FirstSubmissionDataCallback firstSubmissionDataCallback, IConsoleReader consoleReader, CancellationToken ct)
        {
            if (submission.ApplicationCategory == DevCenterApplicationCategory.NotSet)
            {
                var categories = Enum.GetNames(typeof(DevCenterApplicationCategory))
                    .Where(c => c != nameof(DevCenterApplicationCategory.NotSet))
                    .ToArray();

                var categoryString = await consoleReader.SelectionPromptAsync(
                    "Please select the Application Category:",
                    categories,
                    20,
                    ct: ct);

                submission.ApplicationCategory = (DevCenterApplicationCategory)Enum.Parse(typeof(DevCenterApplicationCategory), categoryString);
            }

            if (submission.Listings?.Any() != true)
            {
                submission.Listings = new Dictionary<string, DevCenterListing>();

                AnsiConsole.WriteLine("Let's add listings to your application. Please enter the following information:");

                var listingCount = 0;
                do
                {
                    AnsiConsole.WriteLine("\tHow many listings do you want to add? One is enough, but you might want to support more listing languages.");
                    var listingCountString = await consoleReader.ReadNextAsync(false, ct);
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
                        listingLanguage = await consoleReader.RequestStringAsync("\tEnter the language of the listing (e.g. 'en-US')", false, ct);
                        if (string.IsNullOrEmpty(listingLanguage))
                        {
                            AnsiConsole.WriteLine("Invalid listing language.");
                        }
                    }
                    while (string.IsNullOrEmpty(listingLanguage));

                    var submissionData = await firstSubmissionDataCallback(listingLanguage, ct);

                    var listing = new DevCenterListing
                    {
                        BaseListing = new BaseListing
                        {
                            Title = app.PrimaryName,
                            Description = submissionData.Description ?? await consoleReader.RequestStringAsync($"\tEnter the description of the listing ({listingLanguage}):", false, ct),
                        }
                    };

                    if (listing.BaseListing.Images?.Any() != true)
                    {
                        listing.BaseListing.Images = new List<Image>();

                        foreach (var image in submissionData.Images)
                        {
                            listing.BaseListing.Images.Add(new Image
                            {
                                FileName = image.FileName,
                                FileStatus = FileStatus.PendingUpload,
                                ImageType = image.ImageType.ToString()
                            });
                        }
                    }

                    submission.Listings.Add(listingLanguage, listing);
                }
            }

            if (submission.AllowTargetFutureDeviceFamilies == null)
            {
                submission.AllowTargetFutureDeviceFamilies = new Dictionary<string, bool>();
            }

            void UpdateKeyIfNotSet(string key, bool value)
            {
                if (!submission.AllowTargetFutureDeviceFamilies.ContainsKey(key))
                {
                    submission.AllowTargetFutureDeviceFamilies[key] = value;
                }
            }

            UpdateKeyIfNotSet("Desktop", true);
            UpdateKeyIfNotSet("Mobile", false);
            UpdateKeyIfNotSet("Holographic", true);
            UpdateKeyIfNotSet("Xbox", false);
        }
    }
}
