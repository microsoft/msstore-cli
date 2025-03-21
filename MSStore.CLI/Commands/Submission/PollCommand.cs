// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class PollCommand : Command
    {
        public PollCommand()
            : base("poll", "Polls until the existing submission is PUBLISHED or FAILED.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
        }

        public new class Handler(ILogger<PollCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, TelemetryClient telemetryClient, IAnsiConsole ansiConsole, IBrowserLauncher browserLauncher) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));

            public string ProductId { get; set; } = null!;

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                IStorePackagedAPI? storePackagedAPI = null;
                DevCenterSubmission? submission = null;

                var publishingStatus = await _ansiConsole.Status().StartAsync<object?>("Polling submission status", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{ProductId}'");
                                return -1;
                            }

                            submission = await storePackagedAPI.GetAnySubmissionAsync(_ansiConsole, application, ctx, _logger, ct);

                            if (submission?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find submission for application with ID '{ProductId}'");
                                return -1;
                            }

                            var lastSubmissionStatus = await storePackagedAPI.PollSubmissionStatusAsync(_ansiConsole, ProductId, null, submission.Id, false, _logger, ct: ct);

                            ctx.SuccessStatus(_ansiConsole);

                            return lastSubmissionStatus;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var status = await storeAPI.GetModuleStatusAsync(ProductId, ct);

                            if (status?.ResponseData?.OngoingSubmissionId == null ||
                                status.ResponseData.OngoingSubmissionId.Length == 0)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find ongoing submission for application with ID '{ProductId}'");

                                return null;
                            }

                            ResponseWrapper<SubmissionStatus>? lastSubmissionStatus = null;
                            await foreach (var submissionStatus in storeAPI.PollSubmissionStatusAsync(ProductId, status.ResponseData.OngoingSubmissionId, false, ct))
                            {
                                _ansiConsole.MarkupLine($"Submission Status - [green]{submissionStatus.ResponseData?.PublishingStatus}[/]");
                                if (submissionStatus.Errors != null)
                                {
                                    StatusDetailsExtensions.PrintErrorsTable(_ansiConsole, submissionStatus.Errors
                                        .Select(e => new CodeAndDetail
                                        {
                                            Code = e.Code,
                                            Details = e.Message
                                        }));
                                }

                                _ansiConsole.WriteLine();

                                lastSubmissionStatus = submissionStatus;
                            }

                            ctx.SuccessStatus(_ansiConsole);

                            return lastSubmissionStatus;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while polling submission status");
                        ctx.ErrorStatus(_ansiConsole, err);
                    }

                    return null;
                });

                if (publishingStatus is DevCenterSubmissionStatusResponse lastSubmissionStatus)
                {
                    if (storePackagedAPI == null ||
                        submission?.Id == null)
                    {
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                    }

                    return await _telemetryClient.TrackCommandEventAsync<Handler>(
                        ProductId,
                        await storePackagedAPI.HandleLastSubmissionStatusAsync(_ansiConsole, lastSubmissionStatus, ProductId, null, submission.Id, _browserLauncher, _logger, ct),
                        ct);
                }
                else if (publishingStatus is ResponseWrapper<SubmissionStatus> lastSubmissionStatusWrapper)
                {
                    if (lastSubmissionStatusWrapper.ResponseData?.PublishingStatus == PublishingStatus.FAILED)
                    {
                        _ansiConsole.WriteLine("Submission has failed.");

                        if (lastSubmissionStatusWrapper.Errors != null)
                        {
                            foreach (var error in lastSubmissionStatusWrapper.Errors)
                            {
                                _logger.LogError("Could not retrieve submission. Please try again.");
                                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                            }
                        }

                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                    }
                    else
                    {
                        _ansiConsole.WriteLine("Submission commit success!");

                        return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
                    }
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
            }
        }
    }
}
