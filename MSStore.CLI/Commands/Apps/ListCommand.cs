// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Helpers;
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

        public new class Handler(ILogger<ListCommand.Handler> logger, IStoreAPIFactory storeAPIFactory, IAnsiConsole ansiConsole, TelemetryClient telemetryClient) : ICommandHandler
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var appList = await _ansiConsole.Status().StartAsync("Retrieving Managed Applications", async ctx =>
                {
                    try
                    {
                        var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                        var appList = await storePackagedAPI.GetApplicationsAsync(ct);

                        ctx.SuccessStatus(_ansiConsole, "[bold green]Retrieved Managed Applications[/]");

                        return appList;
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving Managed Applications.");
                        ctx.ErrorStatus(_ansiConsole, err);
                        return null;
                    }
                });

                if (appList?.Count > 0)
                {
                    var table = new Table();
                    table.AddColumns(string.Empty, "ProductId", "Display Name", "PackageId");

                    int i = 1;
                    foreach (var p in appList)
                    {
                        table.AddRow(
                            i.ToString(CultureInfo.InvariantCulture),
                            $"[bold u]{p.Id.EscapeMarkup()}[/]",
                            $"[bold u]{p.PrimaryName.EscapeMarkup()}[/]",
                            $"[bold u]{p.PackageIdentityName.EscapeMarkup()}[/]");
                        i++;
                    }

                    AnsiConsole.Write(table);
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(0, ct);
                }

                AnsiConsole.MarkupLine("Your account has [bold][u]no[/] Managed apps[/].");
                return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, ct);
            }
        }
    }
}
