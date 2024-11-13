// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class DeleteCommand : Command
    {
        internal static readonly Option<bool> NoConfirmOption;

        static DeleteCommand()
        {
            NoConfirmOption = new Option<bool>(
                "--no-confirm",
                () => false,
                "Do not prompt for confirmation.");
        }

        public DeleteCommand()
            : base("delete", "Deletes the pending submission from the store.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);

            AddOption(NoConfirmOption);
        }

        public new class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;

            public bool? NoConfirm { get; set; }

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
                    return -2;
                }

                var success = await storePackagedAPI.DeleteSubmissionAsync(ProductId, null, submissionId, _browserLauncher, _logger, ct);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, success ? 0 : -1, ct);
            }
        }
    }
}
