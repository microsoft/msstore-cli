// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
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
    }
}
