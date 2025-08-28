// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
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
            NoConfirmOption = new Option<bool>("--no-confirm")
            {
                DefaultValueFactory = _ => false,
                Description = "Do not prompt for confirmation."
            };
        }

        public DeleteCommand()
            : base("delete", "Deletes the pending submission from the store.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);

            Options.Add(NoConfirmOption);
        }

        public class Handler(ILogger<DeleteCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IConsoleReader consoleReader, IBrowserLauncher browserLauncher, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var noConfirm = parseResult.GetRequiredValue(NoConfirmOption);

                if (ProductTypeHelper.Solve(productId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                API.Packaged.IStorePackagedAPI storePackagedAPI = null!;

                var submissionId = await _ansiConsole.Status().StartAsync("Retrieving Application and Submission", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                        if (application?.Id == null)
                        {
                            ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{productId}'");
                            return null;
                        }

                        if (application.PendingApplicationSubmission?.Id != null)
                        {
                            ctx.SuccessStatus(_ansiConsole, $"Found [green]Pending Submission[/].");
                            return application.PendingApplicationSubmission.Id;
                        }

                        return null;
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not delete the submission. Please check the ProductId.");
                            _logger.LogError(err, "Could not delete the submission. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while deleting submission.");
                            _logger.LogError(err, "Error while deleting application's submission.");
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

                if (submissionId == null)
                {
                    _ansiConsole.WriteLine("No pending submission found.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                _ansiConsole.WriteLine($"Found Pending Submission with Id '{submissionId}'");
                if (noConfirm == false && !await _consoleReader.YesNoConfirmationAsync("Do you want to delete the pending submission?", ct))
                {
                    return -2;
                }

                var success = await storePackagedAPI.DeleteSubmissionAsync(_ansiConsole, productId, null, submissionId, _browserLauncher, _logger, ct);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, success ? 0 : -1, ct);
            }
        }
    }
}
