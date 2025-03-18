// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class CLIConfigurator(
        IStoreAPIFactory storeAPIFactory,
        IConsoleReader consoleReader,
        ICredentialManager credentialManager,
        IConfigurationManager<Configurations> configurationManager,
        IGraphClient graphClient,
        IBrowserLauncher browserLauncher,
        IPartnerCenterManager partnerCenterManager,
        ITokenManager tokenManager,
        ILogger<CLIConfigurator> logger) : ICLIConfigurator
    {
        private readonly IStoreAPIFactory _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
        private readonly IConsoleReader _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
        private readonly ICredentialManager _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
        private readonly IConfigurationManager<Configurations> _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        private readonly IGraphClient _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
        private readonly IBrowserLauncher _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        private readonly IPartnerCenterManager _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
        private readonly ITokenManager _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<bool> ConfigureAsync(IAnsiConsole ansiConsole, bool askConfirmation, Guid? tenantId = null, string? sellerId = null, Guid? clientId = null, string? clientSecret = null, string? certificateThumbprint = null, string? certificateFilePath = null, string? certificatePassword = null, CancellationToken ct = default)
        {
            if (askConfirmation &&
                !await _consoleReader.YesNoConfirmationAsync(
                    "Are you sure you want to reconfigure the msstore credentials?", ct))
            {
                return false;
            }

            MicrosoftStoreCLI.WelcomeMessage(ansiConsole);
            ansiConsole.MarkupLine("Use of the Microsoft Store Developer CLI is subject to the terms of the Microsoft Privacy Statement: [link]https://aka.ms/privacy[/]");
            ansiConsole.WriteLine("You might need to provide some credentials to call the Microsoft Store APIs.");
            ansiConsole.MarkupLine("[bold green]Let's start![/]");
            ansiConsole.WriteLine();

            var config = new Configurations();

            Organization? organization = null;

            if (tenantId == null)
            {
                organization = await GetOrganizationAsync(ansiConsole, config, true, ct);
                if (organization == null)
                {
                    return false;
                }
            }
            else
            {
                config.TenantId = tenantId;
            }

            if (!config.TenantId.HasValue)
            {
                return false;
            }

            if (clientId != null)
            {
                config.ClientId = clientId;
            }

            if (certificateThumbprint != null)
            {
                config.CertificateThumbprint = certificateThumbprint;
            }

            if (certificateFilePath != null)
            {
                config.CertificateFilePath = certificateFilePath;
            }

            if (config.ClientId == null || (clientSecret == null &&
                                        config.CertificateThumbprint == null &&
                                        config.CertificateFilePath == null))
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

                if (!_graphClient.Enabled || await _consoleReader.YesNoConfirmationAsync("Do you have a client+secret?", ct))
                {
                    if (!config.ClientId.HasValue)
                    {
                        if (!_graphClient.Enabled)
                        {
                            if (machineName != null)
                            {
                                displayName = GetDisplayName(machineName);
                            }
                            else
                            {
                                // Fallback in case we can't get the machine's name
                                displayName = GetDisplayName(RandomString());
                            }

                            organization = await GetOrganizationAsync(ansiConsole, config, organization == null, ct);
                            if (organization == null)
                            {
                                return false;
                            }

                            ansiConsole.WriteLine();

                            ansiConsole.MarkupLine("We are going to open the Partner Center website, at the right page.");
                            var domainsString = string.Empty;
                            if (organization.Domain != null)
                            {
                                domainsString = organization.Domain;
                            }

                            await OpenPartnerCenterUserManagementPageAsync("usermanagement#apps", ct);

                            var randomString = RandomString();

                            ansiConsole.MarkupLine("1) At [b green]Account settings[/]/[b green]User management[/]/[b green]Microsoft Entra applications[/] page...");
                            ansiConsole.MarkupLine($"2) Make sure you are signed-in with your administrator account from this domain: [b green]{domainsString}[/].");
                            ansiConsole.MarkupLine("3) Click on [b green]Microsoft Entra applications[/].");
                            ansiConsole.MarkupLine("4) Click on [b green]Add Microsoft Entra Application[/].");
                            ansiConsole.MarkupLine("5) Select [b green]Create Microsoft Entra Application[/] and click on [b green]Continue[/].");
                            ansiConsole.MarkupLine("6) Fill the form. Here are some values that can help you with the setup:");
                            ansiConsole.MarkupLine($"   - Name: [b green]{displayName}[/]");
                            ansiConsole.MarkupLine($"   - Reply URI: [b green]https://{organization.Domain}/MSStoreCLIAccess_{randomString}[/]");
                            ansiConsole.MarkupLine($"   - App ID URI: [b green]https://{organization.Domain}/MSStoreCLIAccess_{randomString}[/]");
                            ansiConsole.MarkupLine("7) Click on [b green]Next[/].");
                            ansiConsole.MarkupLine("8) Select [b green]Manager(Windows)[/] and click on [b green]Create[/].");
                            ansiConsole.MarkupLine("9) Copy the GUID from the application that you just created and paste it here:");
                        }

                        string? guidStr = await _consoleReader.RequestStringAsync("Client Id", false, ct);
                        Guid guid;
                        if (!Guid.TryParse(guidStr, out guid))
                        {
                            ansiConsole.MarkupLine("[bold red]Invalid Client Id[/]");
                            return false;
                        }

                        config.ClientId = guid;

                        if (!_graphClient.Enabled)
                        {
                            ansiConsole.MarkupLine("Now click on the application that you just provided the GUID for, and click on [b green]Add new key[/]");
                            ansiConsole.MarkupLine("Copy the [b green]Key[/] value and paste it here:");
                        }
                    }

                    clientSecret = await _consoleReader.RequestStringAsync("Client Secret", true, ct);
                    if (string.IsNullOrEmpty(clientSecret))
                    {
                        ansiConsole.MarkupLine("[bold red]Invalid Client Secret[/]");
                        return false;
                    }
                }
                else
                {
                    if (machineName != null)
                    {
                        displayName = GetDisplayName(machineName);

                        // Search for an existing App, so we don't conflict with other apps display names.
                        var existingAzureApp = await RetrieveApplicationAsync(ansiConsole, displayName, ct);

                        if (existingAzureApp != null)
                        {
                            ansiConsole.MarkupLine($"Found an Azure App with the same display name ([u]{displayName}[/]), using a random one.");
                            displayName = GetDisplayName($"{machineName} - {RandomString()}");
                        }
                    }
                    else
                    {
                        // Fallback in case we can't get the machine's name
                        displayName = GetDisplayName(RandomString());
                    }

                    var clientApp = await CreateClientIdAsync(ansiConsole, displayName, ct);
                    if (clientApp?.Id == null || clientApp?.AppId == null)
                    {
                        return false;
                    }

                    organization = await GetOrganizationAsync(ansiConsole, config, organization == null, ct);
                    if (organization == null)
                    {
                        return false;
                    }

                    var appUpdateRequest = new AppUpdateRequest
                    {
                        IdentifierUris = [$"https://{organization.Domain}/{clientApp.AppId}"],
                        SignInAudience = "AzureADMyOrg"
                    };

                    if (!await UpdateClientAppAsync(ansiConsole, clientApp.Id, appUpdateRequest, ct))
                    {
                        return false;
                    }

                    if (clientApp.AppId == null)
                    {
                        return false;
                    }

                    config.ClientId = clientApp.AppId;
                    var newClientSecret = await CreateClientSecretAsync(ansiConsole, clientApp.Id, displayName, ct);
                    if (string.IsNullOrEmpty(newClientSecret))
                    {
                        return false;
                    }

                    ansiConsole.WriteLine();

                    clientSecret = newClientSecret;

                    ansiConsole.MarkupLine("At the Partner Center website, at the [b green]Account settings[/]/[b green]User management[/]/[b green]Microsoft Entra applications[/] page:");
                    var domainsString = string.Empty;
                    if (organization.Domain != null)
                    {
                        domainsString = organization.Domain;
                    }

                    ansiConsole.MarkupLine($"1) Signin with your administrator account from this domain: [b green]{domainsString}[/].");
                    ansiConsole.MarkupLine("2) Click on [b green]Add Microsoft Entra Application[/].");
                    ansiConsole.MarkupLine("3) Select [b green]Add Microsoft Entra Application[/] and click on [b green]Continue[/].");
                    ansiConsole.MarkupLine($"4) Select the app that we just created for you: [bold green]{displayName}[/] and click on [b green]Next[/].");
                    ansiConsole.MarkupLine("5) Select [b green]Manager(Windows)[/] and click on [b green]Add[/].");
                    ansiConsole.MarkupLine("6) Return here.");
                    ansiConsole.WriteLine();

                    bool yesNo;
                    do
                    {
                        await OpenPartnerCenterUserManagementPageAsync("usermanagement#apps", ct);

                        yesNo = await _consoleReader.YesNoConfirmationAsync("Have you finished the steps above?", ct);
                    }
                    while (!yesNo);
                }
            }

            if (!config.ClientId.HasValue)
            {
                return false;
            }

            if (clientSecret != null)
            {
                _credentialManager.WriteCredential(config.ClientId.Value.ToString(), clientSecret);
            }
            else if (certificatePassword != null)
            {
                _credentialManager.WriteCredential(config.ClientId.Value.ToString(), certificatePassword);
            }
            else
            {
                _credentialManager.ClearCredentials(config.ClientId.Value.ToString());
            }

            config.SellerId = await RetrieveSellerId(ansiConsole, sellerId, ct);
            if (config.SellerId == null)
            {
                ansiConsole.MarkupLine("At the Partner Center website, at the [b green]Account settings[/]/[b green]Organization profile[/]/[b green]Legal info[/] page:");
                await OpenPartnerCenterUserManagementPageAsync("organization/legalinfo#mpn", ct);

                ansiConsole.MarkupLine("Copy the [b green]Seller Id[/] value here:");

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

            ansiConsole.WriteLine();

            var result = await ansiConsole.Status().StartAsync("Testing configuration...", async ctx =>
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
                        catch (Exception ex)
                        {
                            _logger.LogInformation(ex, "Error while creating StoreAPI.");
                            if (i + 1 == maxRetry)
                            {
                                break;
                            }

                            ansiConsole.MarkupLine($"Failed to auth... Might just need to wait a little bit. Retrying again in [b green]{delay}[/] seconds([b green]{i + 1}/{maxRetry}[/])...");
                            await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                        }
                    }

                    if (storeAPI == null)
                    {
                        ctx.ErrorStatus(ansiConsole, "Really failed to auth.");

                        return false;
                    }

                    await _configurationManager.SaveAsync(config, ct);
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while testing the configuration.");
                    ctx.ErrorStatus(ansiConsole, "Ops, something doesn't seem to be right!");
                    return false;
                }

                return true;
            });

            if (result)
            {
                ansiConsole.WriteLine("Configuration saved!");
                ansiConsole.MarkupLine("[bold green]Awesome! It seems to be working![/]");
            }

            return result;
        }

        private async Task OpenPartnerCenterUserManagementPageAsync(string specificPage, CancellationToken ct)
        {
            await _browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/account/v3/{specificPage}", true, ct);
        }

        private async Task<Organization?> GetOrganizationAsync(IAnsiConsole ansiConsole, Configurations config, bool forceSelection, CancellationToken ct)
        {
            if (_tokenManager.CurrentUser == null)
            {
                // Try to use cached account
                await _tokenManager.SelectAccountAsync(false, forceSelection, ct);
            }

            var organization = await GetSignedInOrganizationAsync(ansiConsole, ct);

            if (organization == null)
            {
                return null;
            }

            if (organization.Id == Guid.Empty)
            {
                ansiConsole.MarkupLine("[yellow]This account is either:[/]");
                ansiConsole.MarkupLine("[b]1) [/][yellow]Not registered as a [green]Microsoft Store Developer[/][/]; and/or");
                ansiConsole.MarkupLine("[b]2) [/][yellow]Not associated with an [green]Microsoft Entra Tenant[/].[/]");

                if (await _consoleReader.YesNoConfirmationAsync("Do you know if you are already registed?", ct))
                {
                    ansiConsole.MarkupLine("Let's then associate your account with an [green]Microsoft Entra Tenant[/].");

                    ansiConsole.WriteLine();

                    ansiConsole.MarkupLine("We can't automatically do this...");
                    ansiConsole.MarkupLine("At the Partner Center website, at the [b green]Account settings[/]/[b green]Organization profile[/]/[b green]Tenants[/] page:");
                    ansiConsole.MarkupLine("1) Either associate a Microsoft Entra ID with your Partner Center account, or Create a new Microsoft Entra ID.");
                    ansiConsole.MarkupLine("2) Then close the browser and return here.");
                    ansiConsole.WriteLine();

                    bool yesNo;
                    do
                    {
                        await _browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/account/TenantSetup", true, ct);

                        yesNo = await _consoleReader.YesNoConfirmationAsync("Have you finished the steps above?", ct);
                    }
                    while (!yesNo);

                    await _tokenManager.SelectAccountAsync(false, true, ct);
                }
                else
                {
                    await _browserLauncher.OpenBrowserAsync($"https://partner.microsoft.com/dashboard/registration", true, ct);
                }

                return null;
            }

            if (config.TenantId != null && organization.Id != config.TenantId)
            {
                throw new InvalidOperationException($"Could not find Graph Organization/Tenant with Id [b green]{config.TenantId}[/] in signed in user.");
            }

            if (organization.Id != config.TenantId)
            {
                ansiConsole.MarkupLine($"Found Organization/Tenant [green b]{organization.Id}[/]");
            }

            config.TenantId = organization.Id;

            return organization;
        }

        private async Task<int?> RetrieveSellerId(IAnsiConsole ansiConsole, string? sellerId, CancellationToken ct)
        {
            if (sellerId != null)
            {
                return Convert.ToInt32(sellerId, CultureInfo.InvariantCulture);
            }

            if (!_partnerCenterManager.Enabled)
            {
                return null;
            }

            if (_tokenManager.CurrentUser == null)
            {
                await _tokenManager.SelectAccountAsync(false, true, ct);
            }

            var accountEnrollment = await ansiConsole.Status().StartAsync("Retrieving account enrollment...", async ctx =>
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
                ansiConsole.MarkupLine("Found an enrollment account, using it.");
                return Convert.ToInt32(accountEnrollment?.Id, CultureInfo.InvariantCulture);
            }
            else
            {
                // AnsiConsole.MarkupLine("No enrollment account found, so we couln't automatically find your Seller Id.");
                return null;
            }
        }

        private async Task<Organization?> GetSignedInOrganizationAsync(IAnsiConsole ansiConsole, CancellationToken ct)
        {
            var organization = await ansiConsole.Status().StartAsync("Retrieving Organization Id...", async ctx =>
            {
                try
                {
                    if (_tokenManager.CurrentUser == null)
                    {
                        // If no cache, then get an access token, which will ask for credentials interactively
                        await _tokenManager.GetTokenAsync(GraphClient.GraphApplicationReadWriteScope, ct);
                    }

                    if (_tokenManager.CurrentUser == null)
                    {
                        throw new InvalidOperationException("Could not sign-in.");
                    }

                    return new Organization
                    {
                        Id = new Guid(_tokenManager.CurrentUser.HomeAccountId.TenantId),
                        Domain = _tokenManager.CurrentUser.Username.Split("@")[1]
                    };
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while retrieving Organization");
                    ctx.ErrorStatus(ansiConsole, "Error while retrieving Organization.");
                    return null;
                }
            });

            return organization;
        }

        private async Task<AzureApplication?> CreateClientIdAsync(IAnsiConsole ansiConsole, string displayName, CancellationToken ct)
        {
            ansiConsole.WriteLine();

            return await ansiConsole.Status().StartAsync("Creating App ID...", async ctx =>
            {
                try
                {
                    var app = await _graphClient.CreateAppAsync(displayName, ct);

                    var table = new Table
                    {
                        Title = new TableTitle($":check_mark_button: [green]Created Azure App Registration([u]'{displayName}'[/]):[/]")
                    };
                    table.AddColumns("AppId", "Id");
                    table.AddRow($"[bold u]{app.AppId}[/]", $"[bold u]{app.Id}[/]");
                    ansiConsole.Write(table);

                    if (!app.AppId.HasValue)
                    {
                        _logger?.LogError("Error while creating Azure App Registration");
                        ctx.ErrorStatus(ansiConsole, "Error while creating Azure App Registration.");
                        return null;
                    }

                    var principal = await _graphClient.CreatePrincipalAsync(app.AppId.Value.ToString(), ct);

                    _logger.LogInformation("Created Azure App Principal Id: {PrincipalId}", principal.Id);
                    ansiConsole.MarkupLine(":check_mark_button: [green]Created Azure App Principal Id.[/]");

                    return app;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while creating Azure App Registration");
                    ctx.ErrorStatus(ansiConsole, "Error while creating Azure App Registration.");
                    return null;
                }
            });
        }

        private async Task<AzureApplication?> RetrieveApplicationAsync(IAnsiConsole ansiConsole, string displayName, CancellationToken ct)
        {
            ansiConsole.WriteLine();

            return await ansiConsole.Status().StartAsync("Retrieving Azure Application...", async ctx =>
            {
                try
                {
                    var apps = await _graphClient.GetAppsByDisplayNameAsync(displayName, ct);

                    return apps?.Value?.FirstOrDefault();
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while Retrieving Azure Application");
                    ctx.ErrorStatus(ansiConsole, "Error while Retrieving Azure Application.");
                    return null;
                }
            });
        }

        private async Task<bool> UpdateClientAppAsync(IAnsiConsole ansiConsole, string id, AppUpdateRequest updatedApp, CancellationToken ct)
        {
            ansiConsole.WriteLine();

            return await ansiConsole.Status().StartAsync("Updating Client App...", async ctx =>
            {
                try
                {
                    var app = await _graphClient.UpdateAppAsync(id, updatedApp, ct);

                    ansiConsole.MarkupLine($":check_mark_button: [green]App Registration configured![/]");

                    return true;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while Updating Azure App");
                    ctx.ErrorStatus(ansiConsole, "Error while Updating Azure App.");
                    return false;
                }
            });
        }

        private async Task<string?> CreateClientSecretAsync(IAnsiConsole ansiConsole, string clientId, string displayName, CancellationToken ct)
        {
            ansiConsole.WriteLine();

            return await ansiConsole.Status().StartAsync("Creating Client/App Secret...", async ctx =>
            {
                try
                {
                    var secret = await _graphClient.CreateAppSecretAsync(clientId, displayName, ct);

                    return secret.SecretText;
                }
                catch (Exception err)
                {
                    _logger?.LogError(err, "Error while creating Client/App Secret");
                    ctx.ErrorStatus(ansiConsole, "Error while creating Client/App Secret.");
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

                if (config.ClientId.HasValue)
                {
                    _credentialManager.ClearCredentials(config.ClientId.Value.ToString());
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
