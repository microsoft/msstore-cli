// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using MSStore.CLI.Services;

namespace MSStore.CLI.Commands
{
    internal class ReconfigureCommand : Command
    {
        public ReconfigureCommand()
            : base("reconfigure", "Re-configure the Microsoft Store cli.")
        {
            var tenantId = new Option<string>(
                aliases: new string[] { "--tenantId", "-t" },
                description: "Specify the tenant Id that should be used.");

            var reset = new Option<bool>(
                aliases: new string[] { "--reset" },
                description: "Only reset the credentials, without starting over.");

            AddOption(tenantId);
            AddOption(reset);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ICLIConfigurator _cliConfigurator;

            public string? TenantId { get; set; }
            public bool? Reset { get; set; }

            public Handler(ICLIConfigurator cliConfigurator)
            {
                _cliConfigurator = cliConfigurator;
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                if (Reset == true)
                {
                    return await _cliConfigurator.ResetAsync(ct: ct) ? 0 : 1;
                }
                else
                {
                    return await _cliConfigurator.ConfigureAsync(tenantId: TenantId, ct: ct) ? 0 : 1;
                }
            }
        }
    }
}
