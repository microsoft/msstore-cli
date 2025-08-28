// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class UpdateCommand : Command
    {
        private static readonly Argument<string> ProductArgument;

        static UpdateCommand()
        {
            ProductArgument = new Argument<string>("product")
            {
                Description = "The updated JSON product representation."
            };
        }

        public UpdateCommand()
            : base("update", "Updates the existing draft with the provided JSON.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Arguments.Add(ProductArgument);
            Options.Add(SubmissionCommand.SkipInitialPolling);
        }

        public class Handler(ILogger<UpdateCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public static async Task<object?> PackagedUpdateCommandAsync(IAnsiConsole ansiConsole, IStoreAPIFactory storeAPIFactory, string product, string productId, ILogger logger, CancellationToken ct)
            {
                var updateSubmission = JsonSerializer.Deserialize(product, SourceGenerationContext.GetCustom().DevCenterSubmission);

                if (updateSubmission == null)
                {
                    throw new MSStoreException("Invalid product provided.");
                }

                IStorePackagedAPI storePackagedAPI = null!;

                var application = await ansiConsole.Status().StartAsync("Retrieving existing application", async ctx =>
                {
                    try
                    {
                        storePackagedAPI = await storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                        if (application?.Id == null)
                        {
                            throw new MSStoreException($"Could not find application with ID '{productId}'");
                        }

                        return application;
                    }
                    catch (Exception err)
                    {
                        logger.LogError(err, "Error while updating submission product.");
                        ctx.ErrorStatus(ansiConsole, err);
                        return null;
                    }
                });

                if (storePackagedAPI == null || application == null || application?.Id == null)
                {
                    return 1;
                }

                string? submissionId = application.PendingApplicationSubmission?.Id;

                if (submissionId == null)
                {
                    ansiConsole.MarkupLine("Could not find an existing submission. [b green]Creating new submission[/].");

                    var submission = await storePackagedAPI.CreateNewSubmissionAsync(ansiConsole, application.Id, logger, ct);
                    submissionId = submission?.Id;

                    if (submissionId == null)
                    {
                        throw new MSStoreException("Could not create new submission.");
                    }
                }

                return await ansiConsole.Status().StartAsync("Updating submission product", async ctx =>
                {
                    try
                    {
                        return await storePackagedAPI.UpdateSubmissionAsync(application.Id, submissionId, updateSubmission, ct);
                    }
                    catch (Exception err)
                    {
                        logger.LogError(err, "Error while updating submission product.");
                        ctx.ErrorStatus(ansiConsole, err);
                        return null;
                    }
                });
            }

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var product = parseResult.GetRequiredValue(ProductArgument);
                var skipInitialPolling = parseResult.GetRequiredValue(SubmissionCommand.SkipInitialPolling);

                object? updateSubmissionData = null;

                if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                {
                    updateSubmissionData = await PackagedUpdateCommandAsync(_ansiConsole, _storeAPIFactory, product, productId, _logger, ct);
                }
                else
                {
                    var updatePackagesRequest = JsonSerializer.Deserialize(product, SourceGenerationContext.GetCustom().UpdatePackagesRequest);

                    if (updatePackagesRequest == null)
                    {
                        throw new MSStoreException("Invalid product provided.");
                    }

                    updateSubmissionData = await _ansiConsole.Status().StartAsync("Updating submission product", async ctx =>
                    {
                        try
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            return await storeAPI.UpdateProductPackagesAsync(productId, updatePackagesRequest, skipInitialPolling, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission product.");
                            ctx.ErrorStatus(_ansiConsole, err);
                            return null;
                        }
                    });
                }

                if (updateSubmissionData == null)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

                return await _telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
