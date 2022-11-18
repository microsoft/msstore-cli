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
        private readonly IConfigurationManager<Configurations> _configurationManager;
        private readonly IGraphClient _graphClient;
        private readonly IBrowserLauncher _browserLauncher;
        private readonly IPartnerCenterManager _partnerCenterManager;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger _logger;

        public CLIConfigurator(
            IStoreAPIFactory storeAPIFactory,
            IConsoleReader consoleReader,
            ICredentialManager credentialManager,
            IConfigurationManager<Configurations> configurationManager,
            IGraphClient graphClient,
            IBrowserLauncher browserLauncher,
            IPartnerCenterManager partnerCenterManager,
            ITokenManager tokenManager,
            ILogger<CLIConfigurator> logger)
        {
            _storeAPIFactory = storeAPIFactory ?? throw new ArgumentNullException(nameof(storeAPIFactory));
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
            _credentialManager = credentialManager ?? throw new ArgumentNullException(nameof(credentialManager));
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _partnerCenterManager = partnerCenterManager ?? throw new ArgumentNullException(nameof(partnerCenterManager));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ConfigureAsync(bool askConfirmation, Guid? tenantId = null, string? sellerId = null, Guid? clientId = null, string? clientSecret = null, CancellationToken ct = default)
        {
            if (askConfirmation &&
                !await _consoleReader.YesNoConfirmationAsync(
                    "Are you sure you want to reconfigure the msstore credentials?", ct))
            {
                return false;
            }

            MicrosoftStoreCLI.WelcomeMessage();
            AnsiConsole.WriteLine("Use of the Microsoft Store CLI is subject to the terms of the Microsoft Privacy Statement: https://aka.ms/privacy");
            AnsiConsole.WriteLine("You might need to provide some credentials to call the Microsoft Store APIs.");
            AnsiConsole.MarkupLine("[bold green]Lets start![/]");
            AnsiConsole.WriteLine();

            var config = new Configurations();

            Organization? organization = null;

            if (tenantId == null)
            {
                if (!_partnerCenterManager.Enabled)
                {
                    string? guidStr = await _consoleReader.RequestStringAsync("Please, provide your TenantId", false, ct);
                    if (!Guid.TryParse(guidStr, out var possibleTenantId))
                    {
                        AnsiConsole.MarkupLine("[bold red]Invalid Tenant Id[/]");
                        return false;
                    }

                    config.TenantId = tenantId = possibleTenantId;
                }
                else
                {
                    organization = await GetOrganizationAsync(config, true, ct);
                    if (organization == null)
                    {
                        return false;
                    }
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

            if (config.ClientId == null || clientSecret == null)
            {
                if (!_graphClient.Enabled || await _consoleReader.YesNoConfirmationAsync("Do you have a client+secret?'", ct))
                {
                    if (!config.ClientId.HasValue)
                    {
                        string? guidStr = await _consoleReader.RequestStringAsync("Client Id", false, ct);
                        Guid guid;
                        if (!Guid.TryParse(guidStr, out guid))
                        {
                            AnsiConsole.MarkupLine("[bold red]Invalid Client Id[/]");
                            return false;
                        }

                        config.ClientId = guid;
                    }

                    clientSecret = await _consoleReader.RequestStringAsync("Client Secret", true, ct);
                    if (string.IsNullOrEmpty(clientSecret))
                    {
                        AnsiConsole.MarkupLine("[bold red]Invalid Client Secret[/]");
                        return false;
                    }
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

                    organization = await GetOrganizationAsync(config, organization == null, ct);
                    if (organization == null)
                    {
                        return false;
                    }

                    var appUpdateRequest = new AppUpdateRequest
                    {
                        IdentifierUris = new List<string> { $"https://{organization.Domain}/{clientApp.AppId}" },
                        SignInAudience = "AzureADMyOrg"
                    };

                    if (!await UpdateClientAppAsync(clientApp.Id, appUpdateRequest, ct))
                    {
                        return false;
                    }

                    if (clientApp.AppId == null)
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
                    if (organization.Domain != null)
                    {
                        domainsString = organization.Domain;
                    }

                    AnsiConsole.WriteLine($"1) Signin with your administrator account from this domain: '{domainsString}'.");
                    AnsiConsole.WriteLine("2) Click on 'Add Azure AD Application'.");
                    AnsiConsole.WriteLine("3) Select 'Add Azure AD Application' and click on 'Continue'.");
                    AnsiConsole.MarkupLine($"4) Select the app that we just created for you: [bold green]{displayName}[/] and click on 'Next'.");
                    AnsiConsole.WriteLine("5) Select 'Manager(Windows)' and click on 'Add'.");
                    AnsiConsole.WriteLine("6) Return here.");
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
            }

            if (!config.ClientId.HasValue)
            {
                return false;
            }

            _credentialManager.WriteCredential(config.ClientId.Value.ToString(), clientSecret);

            config.SellerId = await RetrieveSellerId(sellerId, ct);
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
                        catch (Exception ex)
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

        private async Task<Organization?> GetOrganizationAsync(Configurations config, bool forceSelection, CancellationToken ct)
        {
            if (_tokenManager.CurrentUser == null)
            {
                await _tokenManager.SelectAccountAsync(false, forceSelection, ct);
            }

            var organization = await GetSignedInOrganizationAsync(ct);

            if (organization == null)
            {
                return null;
            }

            if (organization.Id == Guid.Empty)
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

                return null;
            }

            if (config.TenantId != null && organization.Id != config.TenantId)
            {
                throw new InvalidOperationException($"Could not find Graph Organization/Tenant with Id '{config.TenantId}' in signed in user.");
            }

            AnsiConsole.MarkupLine($"Found Organization/Tenant [green b]{organization.Id}[/]");

            config.TenantId = organization.Id;

            return organization;
        }

        private async Task<int?> RetrieveSellerId(string? sellerId, CancellationToken ct)
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

        private async Task<Organization?> GetSignedInOrganizationAsync(CancellationToken ct)
        {
            var organization = await AnsiConsole.Status().StartAsync("Retrieving Organization Id...", async ctx =>
            {
                try
                {
                    if (_tokenManager.CurrentUser == null)
                    {
                        // Try to use cached account
                        await _tokenManager.SelectAccountAsync(false, false, ct);
                    }

                    if (_tokenManager.CurrentUser == null)
                    {
                        // If no cache, then get an access token, which will ask for credentials interactively
                        await _tokenManager.GetTokenAsync(Array.Empty<string>(), ct);
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
                    ctx.ErrorStatus("Error while retrieving Organization.");
                    return null;
                }
            });

            return organization;
        }

        private async Task<AzureApplication?> CreateClientIdAsync(string displayName, CancellationToken ct)
        {
            AnsiConsole.WriteLine();

            return await AnsiConsole.Status().StartAsync("Creating App ID...", async ctx =>
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
                    AnsiConsole.Write(table);

                    if (!app.AppId.HasValue)
                    {
                        _logger?.LogError("Error while creating Azure App Registration");
                        ctx.ErrorStatus("Error while creating Azure App Registration.");
                        return null;
                    }

                    var principal = await _graphClient.CreatePrincipalAsync(app.AppId.Value.ToString(), ct);

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
                    var apps = await _graphClient.GetAppsByDisplayNameAsync(displayName, ct);

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
                    var app = await _graphClient.UpdateAppAsync(id, updatedApp, ct);

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
                    var secret = await _graphClient.CreateAppSecretAsync(clientId, displayName, ct);

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
