// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Submission
{
    internal class GetCommand : Command
    {
        public GetCommand()
            : base("get", "Retrieves the existing draft from the store submission.")
        {
            var module = new Option<string>(
                aliases: new string[] { "--module", "-m" },
                description: "Select which module you want to retrieve ('availability', 'listings' or 'properties').");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddOption(module);
            AddOption(SubmissionCommand.LanguageOption);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

            public string Module { get; set; } = null!;
            public string Language { get; set; } = null!;
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

                var submission = await AnsiConsole.Status().StartAsync("Retrieving Submission", async ctx =>
                {
                    try
                    {
                        object? submission = null;
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application?.Id == null)
                            {
                                ctx.ErrorStatus($"Could not find application with ID '{ProductId}'");
                                return -1;
                            }

                            submission = await storePackagedAPI.GetAnySubmissionAsync(application, ctx, _logger, ct);
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            try
                            {
                                submission = await storeAPI.GetDraftAsync(ProductId, Module, Language, ct);
                            }
                            catch (ArgumentException ex)
                            {
                                ctx.ErrorStatus(ex.Message);
                            }

                            ctx.SuccessStatus();
                        }

                        return submission;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission.");
                        ctx.ErrorStatus(err);
                        return null;
                    }
                });

                if (submission == null)
                {
                    return -1;
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(submission, submission.GetType(), SourceGenerationContext.GetCustom(true)));

                return 0;
            }
        }
    }
}
