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
using MSStore.API.Packaged;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class UpdateCommand : Command
    {
        public UpdateCommand()
            : base("update", "Updates the existing draft with the provided JSON.")
        {
            var product = new Argument<string>(
                "product",
                description: "The updated JSON product representation.");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(product);
            AddOption(SubmissionCommand.SkipInitialPolling);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

            public string Product { get; set; } = null!;
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

                object? updateSubmissionData = null;

                if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                {
                    var updateSubmission = JsonSerializer.Deserialize(Product, SourceGenerationContext.GetCustom().DevCenterSubmission);

                    if (updateSubmission == null)
                    {
                        throw new MSStoreException("Invalid product provided.");
                    }

                    IStorePackagedAPI storePackagedAPI = null!;

                    var application = await AnsiConsole.Status().StartAsync("Retrieving existing application", async ctx =>
                    {
                        try
                        {
                            storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                throw new MSStoreException($"Could not find application with ID '{ProductId}'");
                            }

                            return application;
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission product.");
                            ctx.ErrorStatus(err);
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
                        AnsiConsole.MarkupLine("Could not find an existing submission. [b green]Creating new submission[/].");

                        var submission = await storePackagedAPI.CreateNewSubmissionAsync(application.Id, _logger, ct);
                        submissionId = submission?.Id;

                        if (submissionId == null)
                        {
                            throw new MSStoreException($"Could not find submission with ID '{ProductId}'");
                        }
                    }

                    updateSubmissionData = await AnsiConsole.Status().StartAsync("Updating submission product", async ctx =>
                    {
                        try
                        {
                            return await storePackagedAPI.UpdateSubmissionAsync(application.Id, submissionId, updateSubmission, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission product.");
                            ctx.ErrorStatus(err);
                            return null;
                        }
                    });
                }
                else
                {
                    var updatePackagesRequest = JsonSerializer.Deserialize(Product, SourceGenerationContext.GetCustom().UpdatePackagesRequest);

                    if (updatePackagesRequest == null)
                    {
                        throw new MSStoreException("Invalid product provided.");
                    }

                    updateSubmissionData = await AnsiConsole.Status().StartAsync("Updating submission product", async ctx =>
                    {
                        try
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            return await storeAPI.UpdateProductPackagesAsync(ProductId, updatePackagesRequest, SkipInitialPolling, ct);
                        }
                        catch (Exception err)
                        {
                            _logger.LogError(err, "Error while updating submission product.");
                            ctx.ErrorStatus(err);
                            return null;
                        }
                    });
                }

                if (updateSubmissionData == null)
                {
                    return -1;
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(updateSubmissionData, updateSubmissionData.GetType(), SourceGenerationContext.GetCustom(true)));

                return 0;
            }
        }
    }
}
