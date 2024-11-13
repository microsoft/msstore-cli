// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Helpers;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class PublishCommand : Command
    {
        internal static readonly Option<string> FlightIdOption;
        internal static readonly Option<float> PackageRolloutPercentageOption;

        static PublishCommand()
        {
            FlightIdOption = new Option<string>(
                aliases: ["--flightId", "-f"],
                description: "Specifies the Flight Id where the package will be published.");
            PackageRolloutPercentageOption = new Option<float>(
                aliases: ["--packageRolloutPercentage", "-prp"],
                description: "Specifies the rollout percentage of the package. The value must be between 0 and 100.",
                isDefault: false,
                parseArgument: result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return 100f;
                    }

                    string? percentage = result.Tokens.Single().Value;
                    if (!float.TryParse(percentage, out float parsedPercentage))
                    {
                        result.ErrorMessage = "Invalid rollout percentage. The value must be between 0 and 100.";
                        return 100f;
                    }
                    else if (parsedPercentage < 0 || parsedPercentage > 100)
                    {
                        result.ErrorMessage = "Invalid rollout percentage. The value must be between 0 and 100.";
                        return 100f;
                    }
                    else
                    {
                        return parsedPercentage;
                    }
                });
        }

        public PublishCommand()
            : base("publish", "Publishes your Application to the Microsoft Store.")
        {
            AddArgument(InitCommand.PathOrUrl);

            var inputDirectory = new Option<DirectoryInfo?>(
                aliases:
                [
                    "--inputDirectory",
                    "-i"
                ],
                description: "The directory where the '.msix' or '.msixupload' file to be used for the publishing command. If not provided, the cli will try to find the best candidate based on the 'pathOrUrl' argument.",
                parseArgument: result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return null;
                    }

                    string? directoryPath = result.Tokens.Single().Value;
                    if (!Directory.Exists(directoryPath))
                    {
                        result.ErrorMessage = "Input directory does not exist.";
                        return null;
                    }
                    else
                    {
                        return new DirectoryInfo(directoryPath);
                    }
                });

            AddOption(inputDirectory);

            var appIdOption = new Option<string>(
                aliases:
                [
                    "--appId",
                    "-id"
                ],
                description: "Specifies the Application Id. Only needed if the project has not been initialized before with the 'init' command.");

            AddOption(appIdOption);

            var noCommitOption = new Option<bool>(
                aliases:
                [
                    "--noCommit",
                    "-nc"
                ],
                description: "Disables committing the submission, keeping it in draft state.",
                getDefaultValue: () => false);

            AddOption(noCommitOption);

            AddOption(FlightIdOption);

            AddOption(PackageRolloutPercentageOption);
        }

        public new class Handler(
            IProjectConfiguratorFactory projectConfiguratorFactory,
            IStoreAPIFactory storeAPIFactory,
            TelemetryClient telemetryClient,
            ILogger<PublishCommand.Handler> logger) : ICommandHandler
        {
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            public string PathOrUrl { get; set; } = null!;

            public string? AppId { get; set; }

            public string? FlightId { get; set; }
            public float? PackageRolloutPercentage { get; set; }

            public DirectoryInfo? InputDirectory { get; set; } = null!;

            public bool NoCommit { get; set; }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var projectPublisher = await _projectConfiguratorFactory.FindProjectPublisherAsync(PathOrUrl, ct);

                var props = new Dictionary<string, string>();

                if (projectPublisher == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We could not find a project publisher for the project at '{0}'.", PathOrUrl);
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = projectPublisher.ToString() ?? string.Empty;

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                AnsiConsole.WriteLine($"This seems to be a {projectPublisher} project.");

                API.Packaged.Models.DevCenterApplication? app = null;

                if (!string.IsNullOrEmpty(AppId))
                {
                    app = await AnsiConsole.Status().StartAsync("Retrieving application...", async ctx =>
                    {
                        try
                        {
                            var app = await storePackagedAPI.GetApplicationAsync(AppId, ct);

                            ctx.SuccessStatus("Ok! Found the app!");
                            return app;
                        }
                        catch (Exception)
                        {
                            ctx.ErrorStatus("Could not retrieve your application. Please make sure you have the correct AppId.");
                            _logger.LogError("Could not find application with id '{AppId}'.", AppId);
                            return null;
                        }
                    });

                    if (app == null)
                    {
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-2, props, ct);
                    }
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    await projectPublisher.PublishAsync(PathOrUrl, app, FlightId, InputDirectory, NoCommit, PackageRolloutPercentage, storePackagedAPI, ct), props, ct);
            }
        }
    }
}