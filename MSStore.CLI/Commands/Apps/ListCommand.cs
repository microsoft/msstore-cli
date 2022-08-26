// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands.Apps
{
    internal class ListCommand : Command
    {
        public ListCommand()
            : base("list", "List all the apps associated with this account.")
        {
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IStoreAPIFactory _storeAPIFactory;

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

                return await AnsiConsole.Status().StartAsync("Retrieving Managed Applications", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var appList = await storePackagedAPI.GetApplicationsAsync(ct);

                        ctx.SuccessStatus("[bold green]Retrieved Managed Applications[/]");

                        if (appList?.Any() == true)
                        {
                            var table = new Table();
                            table.AddColumns(string.Empty, "ProductId", "Display Name", "PackageId");

                            int i = 1;
                            foreach (var p in appList)
                            {
                                table.AddRow(
                                    i.ToString(CultureInfo.InvariantCulture),
                                    $"[bold u]{p.Id}[/]",
                                    $"[bold u]{p.PrimaryName}[/]",
                                    $"[bold u]{p.PackageIdentityName}[/]");
                                i++;
                            }

                            AnsiConsole.Write(table);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("Your account has [bold][u]no[/] Managed apps[/].");
                        }

                        AnsiConsole.WriteLine();

                        return 0;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Managed Applications.");
                        ctx.ErrorStatus(err);
                        return -1;
                    }
                });
            }
        }
    }
}
