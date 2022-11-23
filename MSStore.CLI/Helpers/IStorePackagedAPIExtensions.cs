// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class IStorePackagedAPIExtensions
    {
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
                        logger?.LogInformation("{Code} - {Details}", error.Code, error.Details);
                    }
                }

                if (submissionStatus.StatusDetails?.CertificationReports?.Any() == true)
                {
                    var table = new Table
                    {
                        Title = new TableTitle($":paperclip: [b]Certification Reports[/]")
                    };
                    table.AddColumns("Date", "Report");
                    foreach (var certificationReport in submissionStatus.StatusDetails.CertificationReports)
                    {
                        var url = $"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}";
                        table.AddRow($"[bold u]{certificationReport.Date}[/]", $"[bold u]{url}[/]");
                    }

                    AnsiConsole.Write(table);
                }

                AnsiConsole.WriteLine();

                lastSubmissionStatus = submissionStatus;
            }

            return lastSubmissionStatus;
        }

        public static async Task<int> HandleLastSubmissionStatusAsync(this IStorePackagedAPI storePackagedAPI, DevCenterSubmissionStatusResponse lastSubmissionStatus, string productId, string submissionId, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, ILogger logger, CancellationToken ct = default)
        {
            if ("CommitFailed".Equals(lastSubmissionStatus?.Status, StringComparison.Ordinal))
            {
                var error = lastSubmissionStatus.StatusDetails?.Errors?.FirstOrDefault();
                if (lastSubmissionStatus.StatusDetails?.Errors?.Count == 1 &&
                    error?.Code == "InvalidMicrosoftAgeRating")
                {
                    AnsiConsole.WriteLine("Submission has failed. For the first submission of a new application, you need to complete the Microsoft Age Rating at the Microsoft Partner Center.");

                    AnsiConsole.WriteLine("Press 'Enter' to open the browser at right page...");

                    await consoleReader.ReadNextAsync(false, ct);

                    browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}/ageratings");
                }
                else if (lastSubmissionStatus.StatusDetails?.Errors?.Count == 1 &&
                    error?.Code == "InvalidState")
                {
                    AnsiConsole.WriteLine("Submission has failed. Submission has active validation errors which cannot be exposed via API.");

                    AnsiConsole.WriteLine("Press 'Enter' to open the browser at right page...");

                    await consoleReader.ReadNextAsync(false, ct);

                    browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}");
                }
                else
                {
                    AnsiConsole.WriteLine("Submission has failed. Please check the Errors collection of the submissionResource response.");
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
                            browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/products/{appId}/submissions/{existingSubmission.Id}");
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
    }
}
