// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading;
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
    internal class GetCommand : Command
    {
        private static readonly Option<string> ModuleOption;

        static GetCommand()
        {
            ModuleOption = new Option<string>("--module", "-m")
            {
                Description = "Select which module you want to retrieve ('availability', 'listings' or 'properties')."
            };
        }

        public GetCommand()
            : base("get", "Retrieves the existing draft from the store submission.")
        {
            Arguments.Add(SubmissionCommand.ProductIdArgument);
            Options.Add(ModuleOption);
            Options.Add(SubmissionCommand.LanguageOption);
        }

        public class Handler(ILogger<GetCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var productId = parseResult.GetRequiredValue(SubmissionCommand.ProductIdArgument);
                var module = parseResult.GetValue(ModuleOption);
                var language = parseResult.GetRequiredValue(SubmissionCommand.LanguageOption);

                var submission = await _ansiConsole.Status().StartAsync("Retrieving Submission", async ctx =>
                {
                    try
                    {
                        object? submission = null;
                        if (ProductTypeHelper.Solve(productId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(productId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus(_ansiConsole, $"Could not find application with ID '{productId}'");
                                return -1;
                            }

                            submission = await storePackagedAPI.GetAnySubmissionAsync(_ansiConsole, application, ctx, _logger, ct);
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            try
                            {
                                submission = await storeAPI.GetDraftAsync(productId, module, language, ct);
                            }
                            catch (ArgumentException ex)
                            {
                                ctx.ErrorStatus(_ansiConsole, ex.Message);
                            }

                            ctx.SuccessStatus(_ansiConsole);
                        }

                        return submission;
                    }
                    catch (MSStoreHttpException err)
                    {
                        if (err.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not find the Application or submission. Please check the ProductId.");
                            _logger.LogError(err, "Could not find the Application or submission. Please check the ProductId.");
                        }
                        else
                        {
                            ctx.ErrorStatus(_ansiConsole, "Error while retrieving submission.");
                            _logger.LogError(err, "Error while retrieving submission for Application.");
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

                if (submission == null)
                {
                    return await telemetryClient.TrackCommandEventAsync<Handler>(productId, -1, ct);
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(submission, submission.GetType(), SourceGenerationContext.GetCustom(true)));

                return await telemetryClient.TrackCommandEventAsync<Handler>(productId, 0, ct);
            }
        }
    }
}
