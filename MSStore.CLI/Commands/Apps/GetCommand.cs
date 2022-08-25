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

namespace MSStore.CLI.Commands.Apps
{
    internal class GetCommand : Command
    {
        public GetCommand()
            : base("get", "Retrieves the Application details.")
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

                return await AnsiConsole.Status().StartAsync("Retrieving Application", async ctx =>
                {
                    try
                    {
                        if (ProductTypeHelper.Solve(ProductId) == ProductType.Packaged)
                        {
                            var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                            var application = await storePackagedAPI.GetApplicationAsync(ProductId, ct);

                            if (application == null)
                            {
                                return -1;
                            }

                            AnsiConsole.WriteLine(JsonSerializer.Serialize(application, application.GetType(), SourceGenerationContext.GetCustom(true)));
                        }
                        else
                        {
                            var storeAPI = await _storeAPIFactory.CreateAsync(ct: ct);

                            AnsiConsole.MarkupLine("[yellow b]Not supported yet![/]");
                        }

                        ctx.SuccessStatus();

                        return 0;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Application.");
                        ctx.ErrorStatus(err);
                        return -1;
                    }
                });
            }
        }
    }
}
