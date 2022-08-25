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
    internal class StatusCommand : Command
    {
        public StatusCommand()
            : base("status", "Retrieves the current status of the store submission.")
        {
            AddArgument(SubmissionCommand.ProductIdArgument);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

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

                var status = await AnsiConsole.Status().StartAsync<object?>("Retrieving submission status", async ctx =>
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
                                return null;
                            }

                            return (await storePackagedAPI.GetAnySubmissionAsync(application, ctx, _logger, ct))?.StatusDetails;
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            return await storeAPI.GetModuleStatusAsync(ProductId, ct);
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving submission status.");
                        AnsiConsole.WriteLine("Error!");
                        return null;
                    }
                });

                if (status == null)
                {
                    return -1;
                }

                AnsiConsole.WriteLine(JsonSerializer.Serialize(status, status.GetType(), SourceGenerationContext.GetCustom(true)));

                return 0;
            }
        }
    }
}
