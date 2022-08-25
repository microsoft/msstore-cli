// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
            var submissionId = new Argument<string>(
                name: "submissionId",
                description: "The submission ID.");

            AddArgument(SubmissionCommand.ProductIdArgument);
            AddArgument(submissionId);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

            public string SubmissionId { get; set; } = null!;
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

                return await AnsiConsole.Status().StartAsync("Publishing submission", async ctx =>
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

                            var submission = await storePackagedAPI.GetAnySubmissionAsync(application, ctx, _logger, ct);

                            if (submission?.Id == null)
                            {
                                ctx.ErrorStatus($"Could not find submission for application with ID '{ProductId}'");
                                return -1;
                            }

                            var submissionCommit = await storePackagedAPI.CommitSubmissionAsync(application.Id, submission.Id, ct);

                            ctx.SuccessStatus($"Submission Commited with status [green u]{submissionCommit.Status}[/]");

                            return 0;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            var submissionId = await storeAPI.PublishSubmissionAsync(ProductId, ct);

                            ctx.SuccessStatus($"Published with Id [green u]{submissionId}[/]");

                            return 0;
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while publishing submission");
                        ctx.ErrorStatus(err);
                        return -1;
                    }
                });
            }
        }
    }
}
