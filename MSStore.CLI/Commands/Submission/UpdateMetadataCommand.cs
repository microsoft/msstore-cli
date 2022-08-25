// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class UpdateMetadataCommand : Command
    {
        public UpdateMetadataCommand()
            : base("updateMetadata", "Updates the existing draft submission metadata with the provided JSON.")
        {
            var metadata = new Argument<string>(
                name: "metadata",
                description: "The updated JSON metadata representation.");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(metadata);
            AddOption(SubmissionCommand.SkipInitialPolling);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

            public string Metadata { get; set; } = null!;
            public bool SkipInitialPolling { get; set; }
            public string ProductId { get; set; } = null!;

            public Handler(ILogger<Handler> logger, IStoreAPIFactory storeAPIFactory)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                return await AnsiConsole.Status().StartAsync("Updating submission metadata", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus($"Could not find application with ID '{ProductId}'");
                                return -1;
                            }

                            // TODO: Update the App Metadata
                            return -2;
                        }
                        else
                        {
                            var submissionMetadata = JsonSerializer.Deserialize(Metadata, SourceGenerationContext.GetCustom().UpdateMetadataRequest);
                            if (submissionMetadata == null)
                            {
                                throw new MSStoreException("Invalid metadata provided.");
                            }

                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var updateSubmissionData = await storeAPI.UpdateSubmissionMetadataAsync(ProductId, submissionMetadata, SkipInitialPolling, ct);

                            AnsiConsole.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

                            return 0;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while updating submission metadata.");

                        ctx.ErrorStatus(err);
                        return -1;
                    }
                });
            }
        }
    }
}
