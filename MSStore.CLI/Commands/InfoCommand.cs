// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class InfoCommand : Command
    {
        public InfoCommand()
            : base("info", "Print existing configuration..")
        {
        }

        public new class Handler : ICommandHandler
        {
            private readonly IConfigurationManager _configurationManager;

            public Handler(IConfigurationManager configurationManager)
            {
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var config = await _configurationManager.LoadAsync(ct: ct);

                AnsiConsole.WriteLine("Current config:");
                AnsiConsole.WriteLine($"\tSeller Id => {config.SellerId}");
                AnsiConsole.WriteLine($"\tTenant Id => {config.TenantId}");
                AnsiConsole.WriteLine($"\tClient Id => {config.ClientId}");

                return 0;
            }
        }
    }
}
