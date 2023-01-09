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
        internal static readonly Argument<string> PathOrUrl;
        internal static readonly Option<DirectoryInfo?> Output;
        internal static readonly Option<IEnumerable<BuildArch>> Arch;

        static InitCommand()
        {
            PathOrUrl = new Argument<string>("pathOrUrl", "The root directory path where the project file is, or a public URL that points to a PWA.");
            PathOrUrl.AddValidator((result) =>
            {
                var pathOrUrl = result.Tokens.Single().Value;

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
                        result.ErrorMessage = $"Directory does not exist: '{pathOrUrl}'.{Environment.NewLine}";
                    }
                }
            });

            Output = new Option<DirectoryInfo?>(
                aliases: new string[] { "--output", "-o" },
                description: "The output directory where the packaged app will be stored. If not provided, the default directory for each different type of app will be used.");

            Arch = new Option<IEnumerable<BuildArch>>(
                aliases: new string[] { "--arch", "-a" },
                description: "The architecture(s) to build for. If not provided, the default architecture for the current OS, and project type, will be used.")
            {
                AllowMultipleArgumentsPerToken = true,
            };
        }

        public InitCommand()
            : base("init", "Helps you setup your application to publish to the Microsoft Store.")
        {
            AddArgument(PathOrUrl);

            var publisherDisplayName = new Option<string>(
                aliases: new string[] { "--publisherDisplayName", "-n" },
                description: "The Publisher Display Name used to configure the application. If provided, avoids an extra APIs call.");

            AddOption(publisherDisplayName);

            var package = new Option<bool>(
                aliases: new string[] { "--package" },
                description: "If supported by the app type, automatically packs the project.");

            AddOption(package);

            var publish = new Option<bool>(
                aliases: new string[] { "--publish" },
                description: "If supported by the app type, automatically publishes the project. Implies '--package true'");

            AddOption(publish);

            AddOption(Output);

            AddOption(Arch);
        }

        public new class Handler : ICommandHandler
        {
            private readonly ILogger _logger;
            private readonly IBrowserLauncher _browserLauncher;
            private readonly IConsoleReader _consoleReader;
            private readonly IProjectConfiguratorFactory _projectConfiguratorFactory;
            private readonly IStoreAPIFactory _storeAPIFactory;
            private readonly ITokenManager _tokenManager;
            private readonly IPartnerCenterManager _partnerCenterManager;
            private readonly IImageConverter _imageConverter;
            private readonly IConfigurationManager<Configurations> _configurationManager;
            private readonly TelemetryClient _telemetryClient;

            public string PathOrUrl { get; set; } = null!;

            public string? PublisherDisplayName { get; set; } = null!;

            public bool? Package { get; set; }

            public bool? Publish { get; set; }

            public DirectoryInfo? Output { get; set; } = null!;

            public IEnumerable<BuildArch>? Arch { get; set; } = null!;

            public Handler(
                ILogger<Handler> logger,
                IBrowserLauncher browserLauncher,
                IConsoleReader consoleReader,
                IProjectConfiguratorFactory projectConfiguratorFactory,
                IStoreAPIFactory storeAPIFactory,
                ITokenManager tokenManager,
                IPartnerCenterManager partnerCenterManager,
                IImageConverter imageConverter,
                IConfigurationManager<Configurations> configurationManager,
                TelemetryClient telemetryClient)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
                _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
                _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
                _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
                _imageConverter = imageConverter ?? throw new ArgumentNullException(nameof(imageConverter));
                _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
                _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            }

            public int Invoke(InvocationContext context)
            {
                return -1001;
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var configurator = await _projectConfiguratorFactory.FindProjectConfiguratorAsync(PathOrUrl, ct);

                var props = new Dictionary<string, string>
                {
                    { "withPDN", (PublisherDisplayName != null).ToString() },
                    { "Package", (Package == true).ToString() },
                    { "Publish", (Publish == true).ToString() }
                };

                if (configurator == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We could not find a project configurator for the project at '{0}'.", PathOrUrl);
                    props["ProjType"] = "NF";
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                }

                props["ProjType"] = configurator.ConfiguratorProjectType;

                var validationResult = configurator.ValidateCommand(PathOrUrl, Output, Package, Publish);

                if (validationResult.HasValue)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(validationResult.Value, props, ct);
                }

                if (string.IsNullOrEmpty(PublisherDisplayName))
                {
                    if (_partnerCenterManager.Enabled)
                    {
                        await _tokenManager.SelectAccountAsync(true, false, ct);

                        AccountEnrollment? account = null;
                        var success = await AnsiConsole.Status().StartAsync("Waiting for browser Sign in", async ctx =>
                        {
                            try
                            {
                                var accounts = await _partnerCenterManager.GetEnrollmentAccountsAsync(ct);

                                account = accounts.Items?.FirstOrDefault();

                                ctx.SuccessStatus("Authenticated!");
                            }
                            catch (Exception err)
                            {
                                _logger.LogError(err, "Error while authenticating.");
                                ctx.ErrorStatus("Could not authenticate. Please try again.");
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

                        account.WriteInfo();

                        if (string.IsNullOrEmpty(account.Name))
                        {
                            AnsiConsole.MarkupLine("Account name is empty.");
                            return await _telemetryClient.TrackCommandEventAsync<Handler>(-3, props, ct);
                        }

                        PublisherDisplayName = account.Name;
                    }
                    else
                    {
                        var config = await _configurationManager.LoadAsync(ct: ct);
                        PublisherDisplayName = config.PublisherDisplayName;

                        if (string.IsNullOrEmpty(PublisherDisplayName))
                        {
                            PublisherDisplayName = await _consoleReader.RequestStringAsync("Please, provide the PublisherDisplayName", false, ct);
                            if (string.IsNullOrEmpty(PublisherDisplayName))
                            {
                                AnsiConsole.MarkupLine("[bold red]Invalid Publisher Display Name[/]");
                                return await _telemetryClient.TrackCommandEventAsync<Handler>(-1, props, ct);
                            }

                            if (config.PublisherDisplayName != PublisherDisplayName)
                            {
                                config.PublisherDisplayName = PublisherDisplayName;
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

                AnsiConsole.WriteLine($"This seems to be a {configurator.ConfiguratorProjectType} project.");

                bool verbose = context.ParseResult.IsVerbose();
                if (verbose)
                {
                    AnsiConsole.WriteLine($"Using PublisherDisplayName: {PublisherDisplayName}");
                }

                AnsiConsole.WriteLine("Lets set it up for you!");
                AnsiConsole.WriteLine();

                var (result, outputDirectory) = await configurator.ConfigureAsync(PathOrUrl, Output, PublisherDisplayName, app, storePackagedAPI, ct);

                if (result != 0)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(result, props, ct);
                }

                if (outputDirectory != null)
                {
                    Output = outputDirectory;
                }

                await configurator.ValidateImagesAsync(PathOrUrl, _imageConverter, _logger, ct);

                outputDirectory = null;
                if (Package == true || Publish == true)
                {
                    var projectPackager = configurator as IProjectPackager;
                    if (projectPackager == null)
                    {
                        AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We can't package this type of project.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-4, props, ct);
                    }

                    var buildArchs = Arch?.Distinct();
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
                        AnsiConsole.MarkupLine("[red]This project type can only be packaged on Windows.[/]");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-6, props, ct);
                    }

                    (result, outputDirectory) = await projectPackager.PackageAsync(PathOrUrl, app, buildArchs, Output, storePackagedAPI, ct);
                }

                if (result != 0)
                {
                    return await _telemetryClient.TrackCommandEventAsync<Handler>(result, props, ct);
                }

                if (Publish == true)
                {
                    var projectPublisher = configurator as IProjectPublisher;
                    if (projectPublisher == null)
                    {
                        AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We can't publish this type of project.");
                        return await _telemetryClient.TrackCommandEventAsync<Handler>(-5, props, ct);
                    }

                    result = await projectPublisher.PublishAsync(PathOrUrl, app, outputDirectory, storePackagedAPI, ct);
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

                if (appList.Any() != true)
                {
                    AnsiConsole.WriteLine("Your account has no registered apps yet.");
                    AnsiConsole.MarkupLine("[b]Lets create one![/]");
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

                var success = await AnsiConsole.Status().StartAsync("Retrieving all registered applications...", async ctx =>
                {
                    try
                    {
                        appList = await storePackagedAPI.GetApplicationsAsync(ct);

                        ctx.SuccessStatus("Ok! Found your apps!");
                    }
                    catch (Exception err)
                    {
                        _logger.LogError(err, "Error while retrieving applications.");
                        ctx.ErrorStatus("Could not retrieve your registered applications. Please try again.");

                        return false;
                    }

                    return true;
                });

                return success ? appList : null;
            }

            private async Task OpenMicrosoftStoreRegistrationPageAsync(CancellationToken ct)
            {
                AnsiConsole.WriteLine("I see that you are not a Microsoft Store Developer just yet.");
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("I'll redirect you to the Microsoft Store Sign-up page.");

                await _browserLauncher.OpenBrowserAsync("https://partner.microsoft.com/dashboard/registration", true, ct);
            }
        }
    }
}
