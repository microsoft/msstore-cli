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
    internal class PublishCommand : Command
    {
        public PublishCommand()
            : base("publish", "Starts the submission process for the existing Draft.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
        }

        public class Handler(ILogger<PublishCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    productId,
                    await _ansiConsole.Status().StartAsync("Publishing submission", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{productId}'");
                                return -1;
                            }

                            var submission = await storePackagedAPI.GetAnySubmissionAsync(_ansiConsole, application, ctx, _logger, ct);

                            if (submission?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find submission for application with ID '{productId}'");
                                return -1;
                            }

                            var submissionCommit = await storePackagedAPI.CommitSubmissionAsync(application.Id, submission.Id, ct);

                            if (submissionCommit == null)
                            {
                                throw new MSStoreException("Submission commit failed");
                            }

                            if (submissionCommit.Status != null)
                            {
                                ctx.SuccessStatus(_ansiConsole, $"Submission Commited with status [green u]{submissionCommit.Status}[/]");
                                return 0;
                            }

                            ctx.ErrorStatus(_ansiConsole, $"Could not commit submission for application with ID '{productId}'");
                            _ansiConsole.MarkupLine($"[red]{submissionCommit.ToErrorMessage()}[/]");

                            return -1;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var submissionId = await storeAPI.PublishSubmissionAsync(productId, ct);

                            if (submissionId == null)
                            {
                                return -1;
                            }

                            ctx.SuccessStatus(_ansiConsole, $"Published with Id [green u]{submissionId}[/]");

                            return 0;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while publishing submission");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return -1;
                    }
                }), ct);
            }
        }
    }
}
