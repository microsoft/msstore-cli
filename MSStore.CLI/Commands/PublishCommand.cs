// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
        internal static readonly Option<bool> ReplacePackagesOption;
        private static readonly Option<DirectoryInfo?> InputDirectoryOption;
        private static readonly Option<string> AppIdOption;
        private static readonly Option<bool> NoCommitOption;

        static PublishCommand()
        {
            FlightIdOption = new Option<string>("--flightId", "-f")
            {
                Description = "Specifies the Flight Id where the package will be published."
            };
            PackageRolloutPercentageOption = new Option<float>("--packageRolloutPercentage", "-prp")
            {
                Description = "Specifies the rollout percentage of the package. The value must be between 0 and 100.",
                CustomParser = result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return 100f;
                    }

                    string? percentage = result.Tokens.Single().Value;
                    if (!float.TryParse(percentage, out float parsedPercentage))
                    {
                        result.AddError("Invalid rollout percentage. The value must be between 0 and 100.");
                        return 100f;
                    }
                    else if (parsedPercentage < 0 || parsedPercentage > 100)
                    {
                        result.AddError("Invalid rollout percentage. The value must be between 0 and 100.");
                        return 100f;
                    }
                    else
                    {
                        return parsedPercentage;
                    }
                }
            };

            ReplacePackagesOption = new Option<bool>("--replacePackages", "-rp")
            {
                Description = "If provided, replaces all app packages",
                DefaultValueFactory = _ => false
            };

            InputDirectoryOption = new Option<DirectoryInfo?>("--inputDirectory", "-i")
            {
                Description = "The directory where the '.msix' or '.msixupload' file to be used for the publishing command. If not provided, the cli will try to find the best candidate based on the 'pathOrUrl' argument.",
                CustomParser = result =>
                {
                    if (result.Tokens.Count == 0)
                    {
                        return null;
                    }

                    string? directoryPath = result.Tokens.Single().Value;
                    if (!Directory.Exists(directoryPath))
                    {
                        result.AddError("Input directory does not exist.");
                        return null;
                    }
                    else
                    {
                        return new DirectoryInfo(directoryPath);
                    }
                }
            };

            AppIdOption = new Option<string>("--appId", "-id")
            {
                Description = "Specifies the Application Id. Only needed if the project has not been initialized before with the 'init' command."
            };

            NoCommitOption = new Option<bool>("--noCommit", "-nc")
            {
                Description = "Disables committing the submission, keeping it in draft state.",
                DefaultValueFactory = _ => false
            };
        }

        public PublishCommand()
            : base("publish", "Publishes your Application to the Microsoft Store.")
        {
            Arguments.Add(InitCommand.PathOrUrlArgument);
            Options.Add(InputDirectoryOption);
            Options.Add(AppIdOption);
            Options.Add(NoCommitOption);
            Options.Add(FlightIdOption);
            Options.Add(PackageRolloutPercentageOption);
            Options.Add(ReplacePackagesOption);
        }

        public class Handler(
            IProjectConfiguratorFactory projectConfiguratorFactory,
            IStoreAPIFactory storeAPIFactory,
            TelemetryClient telemetryClient,
            IAnsiConsole ansiConsole,
            ILogger<PublishCommand.Handler> logger) : AsynchronousCommandLineAction
        {
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var pathOrUrl = parseResult.GetRequiredValue(InitCommand.PathOrUrlArgument);
                var appId = parseResult.GetValue(AppIdOption);
                var flightId = parseResult.GetValue(FlightIdOption);
                var packageRolloutPercentage = parseResult.GetValue(PackageRolloutPercentageOption);
                var replacePackages = parseResult.GetValue(ReplacePackagesOption);

                var inputDirectory = parseResult.GetValue(InputDirectoryOption);
                var noCommit = parseResult.GetRequiredValue(NoCommitOption);

                var projectPublisher = await _projectConfiguratorFactory.FindProjectPublisherAsync(pathOrUrl, ct);

                var props = new Dictionary<string, string>();

                if (projectPublisher == null)
                {
                    _ansiConsole.WriteLine(string.Format(CultureInfo.InvariantCulture, "We could not find a project publisher for the project at '{0}'.", pathOrUrl));
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = projectPublisher.ToString() ?? string.Empty;

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                _ansiConsole.WriteLine($"This seems to be a {projectPublisher} project.");

                API.Packaged.Models.DevCenterApplication? app = null;

                if (!string.IsNullOrEmpty(appId))
                {
                    app = await _ansiConsole.Status().StartAsync("Retrieving application...", async ctx =>
                    {
                        try
                        {
                            var app = await storePackagedAPI.GetApplicationAsync(appId, ct);

                            ctx.SuccessStatus(_ansiConsole, "Ok! Found the app!");
                            return app;
                        }
                        catch (Exception)
                        {
                            ctx.ErrorStatus(_ansiConsole, "Could not retrieve your application. Please make sure you have the correct AppId.");
                            _logger.LogError("Could not find application with id '{AppId}'.", appId);
                            return null;
                        }
                    });

                    if (app == null)
                    {
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-2, props, ct);
                    }
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(
                    await projectPublisher.PublishAsync(pathOrUrl, app, flightId, inputDirectory, noCommit, packageRolloutPercentage, replacePackages, storePackagedAPI, ct),
                    props,
                    ct);
            }
        }
    }
}