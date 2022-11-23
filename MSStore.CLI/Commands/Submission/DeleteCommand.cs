// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class DeleteCommand : Command
    {
        public DeleteCommand()
            : base("delete", "Deletes the pending submission from the store.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);

            var noConfirmOption = new Option<bool>(
                "--no-confirm",
                () => false,
                "Do not prompt for confirmation.");
            AddOption(noConfirmOption);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly IConsoleReader _consoleReader;
            private readonly TelemetryClient _telemetryClient;

            public string ProductId { get; set; } = null!;

            public bool? NoConfirm { get; set; }

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, TelemetryClient telemetryClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Unpackaged)
                {
                    AnsiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                API.Packaged.IStorePackagedAPI storePackagedAPI = null!;

                var submissionId = await AnsiConsole.Status().StartAsync("Retrieving Application and Submission", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                        if (application?.Id == null)
                        {
                            ctx.ErrorStatus($"Could not find application with ID '{ProductId}'");
                            return null;
                        }

                        if (application.PendingApplicationSubmission?.Id != null)
                        {
                            ctx.SuccessStatus($"Found [green]Pending Submission[/].");
                            return application.PendingApplicationSubmission.Id;
                        }

                        return null;
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus("Could not delete the submission. Please check the ProductId.");
                            _logger.LogError(err, "Could not delete the submission. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus("Error while deleting submission.");
                            _logger.LogError(err, "Error while deleting application's submission.");
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

                if (submissionId == null)
                {
                    AnsiConsole.WriteLine("No pending submission found.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine($"Found Pending Submission with Id '{submissionId}'");
                if (NoConfirm == false && !await _consoleReader.YesNoConfirmationAsync("Do you want to delete the pending submission?", ct))
                {
                    return 0;
                }

                var devCenterError = await AnsiConsole.Status().StartAsync("Deleting Submission", async ctx =>
                {
                    try
                    {
                        var devCenterError = await storePackagedAPI.DeleteSubmissionAsync(ProductId, submissionId, ct);
                        ctx.SuccessStatus("Submission deleted successfully.");
                        return devCenterError;
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus("Could not delete the submission. Please check the ProductId.");
                            _logger.LogError(err, "Could not delete the submission. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus("Error while deleting submission.");
                            _logger.LogError(err, "Error while deleting application's submission.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while deleting submission.");
                        ctx.ErrorStatus(err);
                        return null;
                    }
                });

                if (devCenterError != null)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(devCenterError, SourceGenerationContext.GetCustom(true).DevCenterError));
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
