// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Models;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class CLIConfigurator : ICLIConfigurator
    {
        private readonly IStoreAPIFactory _storeAPIFactory;
        private readonly IConsoleReader _consoleReader;
        private readonly ICredentialManager _credentialManager;
        private readonly IConfigurationManager _configurationManager;
        private readonly IGraphClient _azureManagementClient;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IPartnerCenterManager _partnerCenterManager;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger _logger;

        public CLIConfigurator(
            IStoreAPIFactory storeAPIFactory,
            IConsoleReader consoleReader,
            ICredentialManager credentialManager,
            IConfigurationManager configurationManager,
            IGraphClient azureManagementClient,
            IBrowserLauncher browserLauncher,
            IPartnerCenterManager partnerCenterManager,
            ITokenManager tokenManager,
            ILogger<CLIConfigurator> logger)
        {
            _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _azureManagementClient = azureManagementClient ?? throw new ArgumentNullException(nameof(azureManagementClient));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ConfigureAsync(bool askConfirmation = true, string? tenantId = null, CancellationToken ct = default)
        {
            if (askConfirmation &&
                !await _consoleReader.YesNoConfirmationAsync(
                    "Are you sure you want to reconfigure the msstore credentials?", ct))
            {
                return false;
            }

            MicrosoftStoreCLI.WelcomeMessage();
            AnsiConsole.WriteLine("To call the API, you need to provide some credentials.");
            AnsiConsole.MarkupLine("[bold green]Lets start![/]");
            AnsiConsole.WriteLine();

            var config = new Configurations();

            await _tokenManager.SelectAccountAsync(false, true, ct);

            var organizations = await GetAllOrganizationsAsync(ct);

            if (organizations == null)
            {
                return false;
            }

            if (organizations.Any() == false)
            {
                AnsiConsole.MarkupLine("[yellow]This account is either:[/]");
                AnsiConsole.MarkupLine("[b]1) [/][yellow]Not registered as a [green]Microsoft Store Developer[/][/]; and/or");
                AnsiConsole.MarkupLine("[b]2) [/][yellow]Not associated with an [green]Azure AD Tenant[/].[/]");

                if (await _consoleReader.YesNoConfirmationAsync("Do you know if you are already registed?'", ct))
                {
                    AnsiConsole.MarkupLine("Lets then associate your account with an [green]Azure AD Tenant[/].");

                    AnsiConsole.WriteLine();

                    AnsiConsole.WriteLine("We can't automatically do this...");
                    AnsiConsole.WriteLine("At the Partner Center website, at the 'Account settings'/'Organization profile'/'Tenants' page:");
                    AnsiConsole.WriteLine("1) Either associate an Azure AD with your Partner Center account, or Create a new Azure Ad.");
                    AnsiConsole.WriteLine("2) Then close the browser and return here.");
                    AnsiConsole.WriteLine();

                    bool yesNo;
                    do
                    {
                        AnsiConsole.WriteLine("Press 'Enter' to open the browser at the Tenant Setup page...");

                        await _consoleReader.ReadNextAsync(false, ct);

                        _browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/account/TenantSetup");

                        yesNo = await _consoleReader.YesNoConfirmationAsync("Have you finished the steps above?", ct);
                    }
                    while (!yesNo);

                    await _tokenManager.SelectAccountAsync(false, true, ct);
                }
                else
                {
                    AnsiConsole.WriteLine("Press 'Enter' to open the browser at the Account Registration page...");

                    await _consoleReader.ReadNextAsync(false, ct);

                    _browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/registration");
                }

                return false;
            }

            Organization organization = null!;
            var selectedOrganization = await SelectOrganizationAsync(tenantId, organizations, ct);
            if (selectedOrganization == null)
            {
                return false;
            }

            organization = selectedOrganization;
            tenantId = organization.Id;

            if (string.IsNullOrEmpty(tenantId))
            {
                return false;
            }

            config.TenantId = tenantId;

            string clientSecret;
            if (await _consoleReader.YesNoConfirmationAsync("Do you have a client+secret?'", ct))
            {
                config.ClientId = await _consoleReader.RequestStringAsync("Client Id", false, ct);
                clientSecret = await _consoleReader.RequestStringAsync("Client Secret", true, ct);
            }
            else
            {
                string GetDisplayName(string sufix) => $"MSStoreCLIAccess - {sufix}";
                string RandomString() => Path.GetFileNameWithoutExtension(Path.GetRandomFileName());

                string? machineName = null;
                try
                {
                    machineName = Environment.MachineName;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Failed to get machine name");
                }

                string displayName;
                if (machineName != null)
                {
                    displayName = GetDisplayName(machineName);

                    // Search for an existing App, so we don't conflict with other apps display names.
                    var existingAzureApp = await RetrieveApplicationAsync(displayName, ct);

                    if (existingAzureApp != null)
                    {
                        AnsiConsole.MarkupLine($"Found an Azure App with the same display name ([u]{displayName}[/]), using a random one.");
                        displayName = GetDisplayName($"{machineName} - {RandomString()}");
                    }
                }
                else
                {
                    // Fallback in case we can't get the machine's name
                    displayName = GetDisplayName(RandomString());
                }

                var clientApp = await CreateClientIdAsync(displayName, ct);
                if (clientApp?.Id == null || clientApp?.AppId == null)
                {
                    return false;
                }

                var appUpdateRequest = new AppUpdateRequest
                {
                    IdentifierUris = organization.VerifiedDomains?.Select(d => $"https://{d.Name}/{clientApp.AppId}")?.ToList(),
                    SignInAudience = "AzureADMyOrg"
                };

                if (!await UpdateClientAppAsync(clientApp.Id, appUpdateRequest, ct))
                {
                    return false;
                }

                config.ClientId = clientApp.AppId;
                var newClientSecret = await CreateClientSecretAsync(clientApp.Id, displayName, ct);
                if (string.IsNullOrEmpty(newClientSecret))
                {
                    return false;
                }

                AnsiConsole.WriteLine();

                clientSecret = newClientSecret;

                AnsiConsole.WriteLine("We can't automatically do this (yet!)...");
                AnsiConsole.WriteLine("At the Partner Center website, at the 'Account settings'/'User management'/'Azure AD applications' page:");
                var domainsString = string.Empty;
                if (organization.VerifiedDomains != null)
                {
                    domainsString = string.Join(", ", organization.VerifiedDomains.Select(d => d.Name));
                }

                AnsiConsole.WriteLine($"1) Signin with your administrator account from this domain: '{domainsString}'.");
                AnsiConsole.WriteLine("2) Click on 'Create Azure AD application'.");
                AnsiConsole.MarkupLine($"3) Select the app that we just created for you: [bold green]{displayName}[/].");
                AnsiConsole.WriteLine("4) Click 'Next'.");
                AnsiConsole.WriteLine("5) Select 'Manager(Windows)'.");
                AnsiConsole.WriteLine("6) Click 'Add'.");
                AnsiConsole.WriteLine("7) Close the browser and return here.");
                AnsiConsole.WriteLine();

                bool yesNo;
                do
                {
                    AnsiConsole.WriteLine("Press 'Enter' to open the browser at the right page...");

                    await _consoleReader.ReadNextAsync(false, ct);

                    _browserLauncher.OpenBrowser($"https://partner.microsoft.com/dashboard/account/v3/usermanagement#apps");

                    yesNo = await _consoleReader.YesNoConfirmationAsync("Have you finished the steps above?", ct);
                }
                while (!yesNo);
            }

            _credentialManager.WriteCredential(config.ClientId, clientSecret);

            config.SellerId = await RetrieveSellerId(ct);
            if (config.SellerId == null)
            {
                try
                {
                    config.SellerId = Convert.ToInt32(await _consoleReader.RequestStringAsync("Seller Id", false, ct), CultureInfo.InvariantCulture);
                }
                catch
                {
                    _logger.LogError("Seller Id is not a valid number.");
                    return false;
                }
            }

            AnsiConsole.WriteLine();

            var result = await AnsiConsole.Status().StartAsync("Testing configuration...", async ctx =>
            {
                try
                {
                    IStoreAPI? storeAPI = null;
                    int maxRetry = 3;
                    int delay = 10;
                    for (int i = 0; i < maxRetry; i++)
                    {
                        try
                        {
                            storeAPI = await _storeAPIFactory.CreateAsync(config, ct);
                            break;
                        }
                        catch(Exception ex)
                        {
                            _logger.LogInformation(ex, "Error while creating StoreAPI.");
                            if (i + 1 == maxRetry)
                            {
                                break;
                            }

                            AnsiConsole.WriteLine($"Failed to auth... Might just need to wait a little bit. Retrying again in {delay} seconds({i + 1}/{maxRetry})...");
                            await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                        }
                    }

                    if (storeAPI == null)
                    {
                        ctx.ErrorStatus("Really failed to auth.");

                        return false;
                    }

                    await _configurationManager.SaveAsync(config, ct);
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while testing the configuration.");
                    ctx.ErrorStatus("Ops, something doesn't seem to be right!");
                    return false;
                }

                return true;
            });

            if (result)
            {
                AnsiConsole.WriteLine("Configuration saved!");
                AnsiConsole.MarkupLine("[bold green]Awesome! It seems to be working![/]");
            }

            return result;
        }

        private async Task<int?> RetrieveSellerId(CancellationToken ct)
        {
            var accountEnrollment = await AnsiConsole.Status().StartAsync("Retrieving account enrollment...", async ctx =>
            {
                try
                {
                    var enrollmentAccounts = await _partnerCenterManager.GetEnrollmentAccountsAsync(ct);
                    return enrollmentAccounts?.Items?.FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get enrollment accounts");
                    return null;
                }
            });

            if (!string.IsNullOrEmpty(accountEnrollment?.Id))
            {
                _logger.LogInformation("Enrollment account Id: '{AccountEnrollmentId}'", accountEnrollment?.Id);
                AnsiConsole.WriteLine("Found an enrollment account, using it.");
                return Convert.ToInt32(accountEnrollment?.Id, CultureInfo.InvariantCulture);
            }
            else
            {
                // AnsiConsole.WriteLine("No enrollment account found, so we couln't automatically find your Seller Id.");
                return null;
            }
        }

        private async Task<List<Organization>?> GetAllOrganizationsAsync(CancellationToken ct)
        {
            var graphOrganizationsResponse = await AnsiConsole.Status().StartAsync("Retrieving Organization...", async ctx =>
            {
                try
                {
                    return await _azureManagementClient.GetOrganizationsAsync(ct);
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while retrieving Organization");
                    ctx.ErrorStatus("Error while retrieving Organization.");
                    return null;
                }
            });

            if (graphOrganizationsResponse?.Value == null)
            {
                return null;
            }

            var organizations = graphOrganizationsResponse.Value;

            // Remove Temporary test tenant
            /*
            organizations.RemoveAll(t => t.Id == "24f78604-a35c-4dcd-8f0c-054063d014bd");
            */

            return organizations;
        }

        private async Task<Organization?> SelectOrganizationAsync(string? organizationId, List<Organization> organizations, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            if (organizations?.Any() != true)
            {
                return null;
            }

            Organization graphOrganization = null!;
            if (organizationId != null)
            {
                var foundOrg = organizations.FirstOrDefault(t => t.Id == organizationId);

                if (foundOrg == null)
                {
                    throw new InvalidOperationException($"Could not find Graph Organization/Tenant with Id '{organizationId}'");
                }
                else
                {
                    graphOrganization = foundOrg;
                }
            }
            else
            {
                _logger.LogInformation("Tenant Id was not provided, so asking for user sign-in to retrieve it.");

                if (organizations.Count > 1)
                {
                    graphOrganization = await _consoleReader.SelectionPromptAsync(
                            "Which [green]Organization/Tenant[/] do you want to use?",
                            organizations,
                            displaySelector: t => $"[bold]{t.Id}[/]: {t.DisplayName}",
                            ct: ct);
                }
                else
                {
                    graphOrganization = organizations.First();
                }
            }

            AnsiConsole.MarkupLine($"Found Organization/Tenant [green b]{graphOrganization.Id}: {graphOrganization.DisplayName}[/]");

            return graphOrganization;
        }

        private async Task<AzureApplication?> CreateClientIdAsync(string displayName, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Creating App ID...", async ctx =>
            {
                try
                {
                    var app = await _azureManagementClient.CreateAppAsync(displayName, ct);

                    var table = new Table
                    {
                        Title = new TableTitle($":check_mark_button: [green]Created Azure App Registration([u]'{displayName}'[/]):[/]")
                    };
                    table.AddColumns("AppId", "Id");
                    table.AddRow($"[bold u]{app.AppId}[/]", $"[bold u]{app.Id}[/]");
                    AnsiConsole.Write(table);

                    if (app.AppId == null)
                    {
                        _logger?.LogError("Error while creating Azure App Registration");
                        ctx.ErrorStatus("Error while creating Azure App Registration.");
                        return null;
                    }

                    var principal = await _azureManagementClient.CreatePrincipalAsync(app.AppId, ct);

                    _logger.LogInformation("Created Azure App Principal Id: {PrincipalId}", principal.Id);
                    AnsiConsole.MarkupLine(":check_mark_button: [green]Created Azure App Principal Id.[/]");

                    return app;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while creating Azure App Registration");
                    ctx.ErrorStatus("Error while creating Azure App Registration.");
                    return null;
                }
            });
        }

        private async Task<AzureApplication?> RetrieveApplicationAsync(string displayName, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Retrieving Azure Application...", async ctx =>
            {
                try
                {
                    var apps = await _azureManagementClient.GetAppsByDisplayNameAsync(displayName, ct);

                    return apps?.Value?.FirstOrDefault();
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while Retrieving Azure Application");
                    ctx.ErrorStatus("Error while Retrieving Azure Application.");
                    return null;
                }
            });
        }

        private async Task<bool> UpdateClientAppAsync(string id, AppUpdateRequest updatedApp, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Updating Client App...", async ctx =>
            {
                try
                {
                    var app = await _azureManagementClient.UpdateAppAsync(id, updatedApp, ct);

                    AnsiConsole.MarkupLine($":check_mark_button: [green]App Registration configured![/]");

                    return true;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while Updating Azure App");
                    ctx.ErrorStatus("Error while Updating Azure App.");
                    return false;
                }
            });
        }

        private async Task<string?> CreateClientSecretAsync(string clientId, string displayName, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Creating Client/App Secret...", async ctx =>
            {
                try
                {
                    var secret = await _azureManagementClient.CreateAppSecretAsync(clientId, displayName, ct);

                    AnsiConsole.MarkupLine($":check_mark_button: [green]Created Client/App Secret:[/] '[grey u]{secret.SecretText}[/]'");

                    return secret.SecretText;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while creating Client/App Secret");
                    ctx.ErrorStatus("Error while creating Client/App Secret.");
                    return null;
                }
            });
        }

        public async Task<bool> ResetAsync(CancellationToken ct = default)
        {
            if (!await _consoleReader.YesNoConfirmationAsync(
                    "Are you sure you want to reset the MSStore CLI credentials?", ct))
            {
                return false;
            }

            try
            {
                var config = await _configurationManager.LoadAsync(true, ct: ct);

                if (!string.IsNullOrEmpty(config.ClientId))
                {
                    _credentialManager.ClearCredentials(config.ClientId);
                }

                await _configurationManager.ClearAsync(ct);

                await _tokenManager.ClearAllCacheAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while resetting configuration");
                return false;
            }
        }
    }
}
