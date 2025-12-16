// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Helpers;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class InitCommand : Command
    {
        internal static readonly Argument<string> PathOrUrlArgument;
        private static readonly Option<string> PublisherDisplayNameOption;
        private static readonly Option<bool> PackageOption;
        private static readonly Option<bool> PublishOption;
        internal static readonly Option<DirectoryInfo?> OutputOption;
        internal static readonly Option<IEnumerable<BuildArch>> ArchOption;
        internal static readonly Option<Version?> VersionOption;

        static InitCommand()
        {
            PathOrUrlArgument = new Argument<string>("pathOrUrl")
            {
                DefaultValueFactory = _ => Directory.GetCurrentDirectory().ToString(),
                Description = "The root directory path where the project file is, or a public URL that points to a PWA.",
            };
            PathOrUrlArgument.Validators.Add((result) =>
            {
                var pathOrUrl = result.Tokens.SingleOrDefault()?.Value ?? Directory.GetCurrentDirectory().ToString();

                bool IsUri()
                {
                    try
                    {
                        var uri = new Uri(pathOrUrl);

                        return uri.IsAbsoluteUri && !uri.IsFile;
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (!IsUri())
                {
                    DirectoryInfo? directoryPath = new DirectoryInfo(pathOrUrl);
                    if (!directoryPath.Exists)
                    {
                        FileInfo? filePath = new FileInfo(pathOrUrl);
                        if (!filePath.Exists)
                        {
                            result.AddError($"File or directory does not exist: '{pathOrUrl}'.{Environment.NewLine}");
                        }
                    }
                }
            });

            PublisherDisplayNameOption = new Option<string>("--publisherDisplayName", "-n")
            {
                Description = "The Publisher Display Name used to configure the application. If provided, avoids an extra APIs call."
            };

            PackageOption = new Option<bool>("--package")
            {
                Description = "If supported by the app type, automatically packs the project."
            };

            PublishOption = new Option<bool>("--publish")
            {
                Description = "If supported by the app type, automatically publishes the project. Implies '--package true'"
            };

            OutputOption = new Option<DirectoryInfo?>("--output", "-o")
            {
                Description = "The output directory where the packaged app will be stored. If not provided, the default directory for each different type of app will be used."
            };

            ArchOption = new Option<IEnumerable<BuildArch>>("--arch", "-a")
            {
                Description = "The architecture(s) to build for. If not provided, the default architecture for the current OS, and project type, will be used.",
                AllowMultipleArgumentsPerToken = true
            };

            VersionOption = new Option<Version?>("--version", "-ver")
            {
                CustomParser = result =>
                {
                    var version = result.Tokens.Single().Value;
                    if (System.Version.TryParse(version, out var parsedVersion))
                    {
                        return parsedVersion;
                    }

                    result.AddError($"Invalid version: '{version}'.{Environment.NewLine}");
                    return null;
                },
                Description = "The version used when building the app. If not provided, the version from the project file will be used."
            };
        }

        public InitCommand()
            : base("init", "Helps you setup your application to publish to the Microsoft Store.")
        {
            Arguments.Add(PathOrUrlArgument);
            Options.Add(PublisherDisplayNameOption);
            Options.Add(PackageOption);
            Options.Add(PublishOption);
            Options.Add(PublishCommand.FlightIdOption);
            Options.Add(OutputOption);
            Options.Add(ArchOption);
            Options.Add(VersionOption);
            Options.Add(PublishCommand.PackageRolloutPercentageOption);
            Options.Add(PublishCommand.ReplacePackagesOption);
        }

        public class Handler(
            ILogger<InitCommand.Handler> logger,
            IBrowserLauncher browserLauncher,
            IConsoleReader consoleReader,
            IProjectConfiguratorFactory projectConfiguratorFactory,
            IStoreAPIFactory storeAPIFactory,
            ITokenManager tokenManager,
            IPartnerCenterManager partnerCenterManager,
            IImageConverter imageConverter,
            IConfigurationManager<Configurations> configurationManager,
            IAnsiConsole ansiConsole,
            TelemetryClient telemetryClient) : AsynchronousCommandLineAction
        {
            private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
            private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            private readonly ITokenManager _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            private readonly IPartnerCenterManager _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
            private readonly IImageConverter _imageConverter = imageConverter ?? throw new ArgumentNullException(nameof(imageConverter));
            private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            private readonly IAnsiConsole _ansiConsole = ansiConsole ?? throw new ArgumentNullException(nameof(ansiConsole));
            private readonly TelemetryClient _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken ct = default)
            {
                var pathOrUrl = parseResult.GetRequiredValue(PathOrUrlArgument);
                var publisherDisplayName = parseResult.GetValue(PublisherDisplayNameOption);
                var package = parseResult.GetValue(PackageOption);
                var publish = parseResult.GetValue(PublishOption);
                var flightId = parseResult.GetValue(PublishCommand.FlightIdOption);
                var version = parseResult.GetValue(VersionOption);
                var packageRolloutPercentage = parseResult.GetValue(PublishCommand.PackageRolloutPercentageOption);
                var packageReplace = parseResult.GetValue(PublishCommand.ReplacePackagesOption);

                var output = parseResult.GetValue(OutputOption);
                var arch = parseResult.GetValue(ArchOption);

                var configurator = await _projectConfiguratorFactory.FindProjectConfiguratorAsync(pathOrUrl, ct);

                var props = new Dictionary<string, string>
                {
                    {
                        "withPDN", (publisherDisplayName != null).ToString()
                    },
                    {
                        "Package", (package == true).ToString()
                    },
                    {
                        "Publish", (publish == true).ToString()
                    }
                };

                if (configurator == null)
                {
                    _ansiConsole.WriteLine(string.Format(CultureInfo.InvariantCulture, "We could not find a project configurator for the project at '{0}'.", pathOrUrl));
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = configurator.ToString() ?? string.Empty;

                var validationResult = configurator.ValidateCommand(pathOrUrl, output, package, publish);

                if (validationResult.HasValue)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(validationResult.Value, props, ct);
                }

                if (string.IsNullOrEmpty(publisherDisplayName))
                {
                    if (_partnerCenterManager.Enabled)
                    {
                        await _tokenManager.SelectAccountAsync(true, false, ct);

                        AccountEnrollment? account = null;
                        var success = await _ansiConsole.Status().StartAsync("Waiting for browser Sign in", async ctx =>
                        {
                            try
                            {
                                var accounts = await _partnerCenterManager.GetEnrollmentAccountsAsync(ct);

                                account = accounts.Items?.FirstOrDefault();

                                ctx.SuccessStatus(_ansiConsole, "Authenticated!");
                            }
                            catch (Exception err)
                            {
                                _logger.LogError(err, "Error while authenticating.");
                                ctx.ErrorStatus(_ansiConsole, "Could not authenticate. Please try again.");
                                return false;
                            }

                            return true;
                        });

                        if (!success)
                        {
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                        }

                        if (account?.Status != "active")
                        {
                            await OpenMicrosoftStoreRegistrationPageAsync(ct);
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(-2, props, ct);
                        }

                        account.WriteInfo(_ansiConsole);

                        if (string.IsNullOrEmpty(account.Name))
                        {
                            _ansiConsole.MarkupLine("Account name is empty.");
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(-3, props, ct);
                        }

                        publisherDisplayName = account.Name;
                    }
                    else
                    {
                        var config = await _configurationManager.LoadAsync(ct: ct);
                        publisherDisplayName = config.PublisherDisplayName;

                        if (string.IsNullOrEmpty(publisherDisplayName))
                        {
                            publisherDisplayName = await _consoleReader.RequestStringAsync("Please, provide the PublisherDisplayName", false, ct);
                            if (string.IsNullOrEmpty(publisherDisplayName))
                            {
                                _ansiConsole.MarkupLine("[bold red]Invalid Publisher Display Name[/]");
                                return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                            }

                            if (config.PublisherDisplayName != publisherDisplayName)
                            {
                                config.PublisherDisplayName = publisherDisplayName;
                                await _configurationManager.SaveAsync(config, ct);
                            }
                        }
                    }
                }

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                var app = await SelectAppAsync(storePackagedAPI, ct);
                if (app == null || string.IsNullOrEmpty(app.Id))
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                _ansiConsole.WriteLine($"This seems to be a {configurator} project.");

                bool verbose = parseResult.IsVerbose();
                if (verbose)
                {
                    _ansiConsole.WriteLine($"Using PublisherDisplayName: {publisherDisplayName}");
                }

                _ansiConsole.WriteLine("Let's set it up for you!");
                _ansiConsole.WriteLine();

                var (result, outputDirectory) = await configurator.ConfigureAsync(pathOrUrl, output, publisherDisplayName, app, version, storePackagedAPI, ct);

                if (result != 0)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(result, props, ct);
                }

                if (outputDirectory != null)
                {
                    output = outputDirectory;
                }

                await configurator.ValidateImagesAsync(_ansiConsole, pathOrUrl, _imageConverter, _logger, ct);

                outputDirectory = null;
                if (package == true || publish == true)
                {
                    var projectPackager = configurator as IProjectPackager;
                    if (projectPackager == null)
                    {
                        _ansiConsole.WriteLine("We can't package this type of project.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-4, props, ct);
                    }

                    var buildArchs = arch?.Distinct();
                    if (buildArchs?.Any() != true)
                    {
                        buildArchs = projectPackager.DefaultBuildArchs;
                    }

                    if (buildArchs != null)
                    {
                        props["Archs"] = string.Join(",", buildArchs);
                    }

                    if (projectPackager.PackageOnlyOnWindows && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        _ansiConsole.MarkupLine("[red]This project type can only be packaged on Windows.[/]");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-6, props, ct);
                    }

                    (result, outputDirectory) = await projectPackager.PackageAsync(pathOrUrl, app, buildArchs, version, output, storePackagedAPI, ct);
                }

                if (result != 0)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(result, props, ct);
                }

                if (publish == true)
                {
                    var projectPublisher = configurator as IProjectPublisher;
                    if (projectPublisher == null)
                    {
                        _ansiConsole.WriteLine("We can't publish this type of project.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-5, props, ct);
                    }

                    result = await projectPublisher.PublishAsync(pathOrUrl, app, flightId, outputDirectory, false, packageRolloutPercentage, packageReplace, storePackagedAPI, ct);
                }

                return await _telemetryClient.TrackCommandEventAsync<Handler>(result, props, ct);
            }

            private async Task<DevCenterApplication?> SelectAppAsync(IStorePackagedAPI storePackagedAPI, CancellationToken ct)
            {
                var appList = await GetAppListAsync(storePackagedAPI, ct);

                if (appList == null)
                {
                    return null;
                }

                if (appList.Count == 0)
                {
                    _ansiConsole.WriteLine("Your account has no registered apps yet.");
                    _ansiConsole.MarkupLine("[b]Let's create one![/]");
                    return await CreateNewAppAsync(ct);
                }

                var newAppOption = "Create a new app...";

                var appNames = appList.Select(app => app.PrimaryName!).ToList();

                /*
                appNames.Add(newAppOption);
                */

                var selectedApp = await _consoleReader.SelectionPromptAsync(
                    "Which application should we use to configure your project?",
                    appNames,
                    ct: ct);

                return selectedApp == newAppOption
                    ? await CreateNewAppAsync(ct)
                    : appList.FirstOrDefault(app => app.PrimaryName == selectedApp);
            }

            private Task<DevCenterApplication?> CreateNewAppAsync(CancellationToken ct)
            {
                throw new NotImplementedException("App name reservation is not implemented yet.");
            }

            private async Task<List<DevCenterApplication>?> GetAppListAsync(IStorePackagedAPI storePackagedAPI, CancellationToken ct)
            {
                List<DevCenterApplication>? appList = null;

                var success = await _ansiConsole.Status().StartAsync("Retrieving all registered applications...", async ctx =>
                {
                    try
                    {
                        appList = await storePackagedAPI.GetApplicationsAsync(ct);

                        ctx.SuccessStatus(_ansiConsole, "Ok! Found your apps!");
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving applications.");
                        ctx.ErrorStatus(_ansiConsole, "Could not retrieve your registered applications. Please try again.");

                        return false;
                    }

                    return true;
                });

                return success ? appList : null;
            }

            private async Task OpenMicrosoftStoreRegistrationPageAsync(CancellationToken ct)
            {
                _ansiConsole.WriteLine("I see that you are not a Microsoft Store Developer just yet.");
                _ansiConsole.WriteLine();
                _ansiConsole.WriteLine("I'll redirect you to the Microsoft Store Sign-up page.");

                await _browserLauncher.OpenBrowserAsync("https://partner.microsoft.com/dashboard/registration", true, ct);
            }
        }
    }
}