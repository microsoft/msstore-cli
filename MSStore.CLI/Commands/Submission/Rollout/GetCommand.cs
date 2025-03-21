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

namespace MSStore.CLI.Commands.Submission.Rollout
{
    internal class GetCommand : Command
    {
        internal static readonly Option<string> SubmissionIdOption;

        static GetCommand()
        {
            SubmissionIdOption = new Option<string>(
                aliases: ["--submissionId", "-s"],
                description: "The submission ID.");
        }

        public GetCommand()
            : base("get", "Retrieves the rollout status of a submission.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
            AddOption(SubmissionIdOption);
        }

        public new class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public string ProductId { get; set; } = null!;
            public string? SubmissionId { get; set; }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Unpackaged)
                {
                    _ansiConsole.WriteLine("This command is not supported for unpackaged applications.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                var submissionRollout = await _ansiConsole.Status().StartAsync("Retrieving Submission Rollout", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        if (SubmissionId == null)
                        {
                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{ProductId}'");
                                return null;
                            }

                            SubmissionId = application.GetAnySubmissionId();

                            if (SubmissionId == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, "Could not find the submission. Please check the ProductId.");
                                return null;
                            }
                        }

                        return await storePackagedAPI.GetPackageRolloutAsync(ProductId, SubmissionId, null, ct);
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the submission rollout. Please check the ProductId.");
                            _logger.LogError(err, "Could not find the submission rollout. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving submission rollout.");
                            _logger.LogError(err, "Error while retrieving submission rollout for Application.");
                        }

                        return null;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission rollout.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (submissionRollout == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(submissionRollout, SourceGenerationContext.GetCustom(true).PackageRollout));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(ProductId, 0, ct);
            }
        }
    }
}
