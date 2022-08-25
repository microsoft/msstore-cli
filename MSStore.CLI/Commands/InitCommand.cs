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
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Commands.Init.Setup;
using MSStore.CLI.Services;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI.Commands
{
    internal class InitCommand : Command
    {
        public InitCommand()
            : base("init", "Helps you setup your Microsoft Account to be a Microsoft Store Developer.")
        {
            var pathOrUrl = new Argument<string>("pathOrUrl", "The root directory path where the project file is, or a public URL that points to a PWA to be packaged.");
            pathOrUrl.AddValidator(result =>
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
            AddArgument(pathOrUrl);
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

            public string PathOrUrl { get; set; } = null!;

            public Handler(
                ILogger<Handler> logger,
                IBrowserLauncher browserLauncher,
                IConsoleReader consoleReader,
                IProjectConfiguratorFactory projectConfiguratorFactory,
                IStoreAPIFactory storeAPIFactory,
                ITokenManager tokenManager,
                IPartnerCenterManager partnerCenterManager)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
                _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
                _projectConfiguratorFactory = projectConfiguratorFactory ?? throw new ArgumentNullException(nameof(projectConfiguratorFactory));
                _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
                _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
                _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
            }

            public int Invoke(InvocationContext context)
            {
                throw new NotImplementedException();
            }

            public async Task<int> InvokeAsync(InvocationContext context)
            {
                var ct = context.GetCancellationToken();

                var configurator = _projectConfiguratorFactory.FindProjectConfigurator(PathOrUrl);

                if (configurator == null)
                {
                    AnsiConsole.WriteLine(CultureInfo.InvariantCulture, "We could not find a project configurator for the project at '{0}'.", PathOrUrl);
                    return -1;
                }

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
                    return -1;
                }

                if (account?.Status != "active")
                {
                    OpenMicrosoftStoreRegistrationPage();
                    return 0;
                }

                account.WriteInfo();

                var storePackagedAPI = await _storeAPIFactory.CreatePackagedAsync(ct: ct);

                var app = await SelectAppAsync(storePackagedAPI, ct);
                if (app == null || string.IsNullOrEmpty(app.Id))
                {
                    return -1;
                }

                AnsiConsole.WriteLine($"This seems to be a {configurator.ConfiguratorProjectType} project.");
                AnsiConsole.WriteLine("Lets set it up for you!");
                AnsiConsole.WriteLine();

                return await configurator.ConfigureAsync(PathOrUrl, account, app, storePackagedAPI, ct);
            }

            private async Task<DevCenterApplication?> SelectAppAsync(IStorePackagedAPI storePackagedAPI, CancellationToken ct)
            {
                var appList = await GetAppListAsync(storePackagedAPI, ct);

                if (appList?.Any() != true)
                {
                    AnsiConsole.WriteLine("Your account has no registered apps yet.");
                    AnsiConsole.MarkupLine("[b]Lets create one![/]");
                    return await CreateNewAppAsync(ct);
                }

                var newAppOption = "Create a new app...";

                var appNames = appList.Select(app => app.PrimaryName!).ToList();
                appNames.Add(newAppOption);

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

            private void OpenMicrosoftStoreRegistrationPage()
            {
                AnsiConsole.WriteLine("I see that you are not a Microsoft Store Developer just yet.");
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("I'll redirect you to the Microsoft Store Sign-up page.");

                _browserLauncher.OpenBrowser("https://partner.microsoft.com/dashboard/registration");
            }
        }
    }
}
