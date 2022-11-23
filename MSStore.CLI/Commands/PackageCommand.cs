// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using MSStore.CLI.Helpers;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class PackageCommand : Command
    {
        public PackageCommand()
            : base("package", "Helps you package your Microsoft Store Application as an MSIX.")
        {
            AddArgument(InitCommand.PathOrUrl);
            AddOption(InitCommand.Output);
        }

        public new class Handler : ICommandHandler
        {
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly TelemetryClient _telemetryClient;

            public string PathOrUrl { get; set; } = null!;

            public DirectoryInfo? Output { get; set; } = null!;

            public Handler(
                IProjectConfiguratorFactory projectConfiguratorFactory,
                IStoreAPIFactory storeAPIFactory,
                TelemetryClient telemetryClient)
            {
                _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var configurator = _projectConfiguratorFactory.FindProjectConfigurator(PathOrUrl);

                var props = new Dictionary<string, string>();

                if (configurator == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We could not find a project configurator for the project at '{0}'.", PathOrUrl);
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = configurator.ConfiguratorProjectType;

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                AnsiConsole.WriteLine($"This seems to be a {configurator.ConfiguratorProjectType} project.");

                var projectPackager = configurator as IProjectPackager;
                if (projectPackager == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We can't package this type of project.");
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-4, props, ct);
                }

                var (returnCode, outputFile) = await projectPackager.PackageAsync(PathOrUrl, null, Output, storePackagedAPI, ct);

                if (returnCode == 0 && outputFile != null)
                {
                    AnsiConsole.WriteLine($"The packaged app is here:");
                    AnsiConsole.MarkupLine($"[green bold]{outputFile}[/]");
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(returnCode, props, ct);
            }
        }
    }
}
