// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Spectre.Console;

namespace MSStore.CLI.Services.TokenManager
{
    internal class MSALTokenManager : ITokenManager
    {
        private const string MSATenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";

        private static readonly string CacheFileName = "msstore_msal_cache.txt";

        private static readonly string KeyChainServiceName = "msstore_msal_service";
        private static readonly string KeyChainAccountName = "msstore_msal_account";

        private static readonly string LinuxKeyRingSchema = "com.microsoft.store.tokencache";
        private static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        private static readonly string LinuxKeyRingLabel = "MSAL token cache for all Microsoft Store Apps.";
        private static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        private static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "msstore-cli");

        // Microsoft Store CLI
        private static readonly string ClientId = "76b1a1a3-44ef-4bb8-bbe3-cb462ebaeee4";

        private readonly IConsoleReader _consoleReader;

        private IPublicClientApplication? _app;
        private IAccount? _selectedAccount;

        public IAccount? CurrentUser => _selectedAccount;

        public MSALTokenManager(IConsoleReader consoleReader)
        {
            _consoleReader = consoleReader ?? throw new ArgumentNullException(nameof(consoleReader));
        }

        [MemberNotNull(nameof(_app))]
        private async Task InitAppAsync()
        {
            if (_app != null)
            {
                return;
            }

            var cacheDirectory = Path.Combine(MsalCacheHelper.UserRootDirectory, "Microsoft", "MSStore.CLI", "Cache");

            Directory.CreateDirectory(cacheDirectory);

            var storageProperties =
                 new StorageCreationPropertiesBuilder(CacheFileName, cacheDirectory)
                 .WithLinuxKeyring(
                     LinuxKeyRingSchema,
                     LinuxKeyRingCollection,
                     LinuxKeyRingLabel,
                     LinuxKeyRingAttr1,
                     LinuxKeyRingAttr2)
                 .WithMacKeyChain(
                     KeyChainServiceName,
                     KeyChainAccountName)
                 .Build();

            _app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithDefaultRedirectUri()
#if WINDOWS
                .WithBroker()
                .WithParentActivityOrWindow(Helpers.NativeMethods.GetConsoleOrTerminalWindow)
#endif
                .Build();

            // This hooks up the cross-platform cache into MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(_app.UserTokenCache);
        }

        public async Task<AuthenticationResult?> GetTokenAsync(string[] scopes, CancellationToken ct)
        {
            await InitAppAsync();

            AuthenticationResult authenticationResult;

            try
            {
                authenticationResult = await _app.AcquireTokenSilent(scopes, _selectedAccount)
                            .ExecuteAsync(ct);
            }
            catch (MsalUiRequiredException)
            {
                authenticationResult = await _app.AcquireTokenInteractive(scopes)
                            .WithAccount(_selectedAccount)
                            .ExecuteAsync(ct);
            }

            if (_selectedAccount == null)
            {
                _selectedAccount = authenticationResult.Account;
            }

            return authenticationResult;
        }

        public async Task SelectAccountAsync(bool notMSA, bool forceSelection, CancellationToken ct)
        {
            await InitAppAsync();

            var accounts = await _app.GetAccountsAsync();
            if (notMSA)
            {
                accounts = accounts.Where(a => a.HomeAccountId.TenantId != MSATenantId).ToArray();
            }

            IAccount? selectedAccount = null;
            if (accounts.Any())
            {
                if (accounts.Count() == 1)
                {
                    if (forceSelection)
                    {
                        AnsiConsole.WriteLine("You have already signed-in with one account:");
                        AnsiConsole.WriteLine($"\tAccount: {accounts.First().Username}");
                        if (await _consoleReader.YesNoConfirmationAsync("Do you want to use that account now? ('y' to continue and 'n' for New Sign-in/Interactive Mode)", ct))
                        {
                            selectedAccount = accounts.First();
                        }
                    }
                    else
                    {
                        selectedAccount = accounts.First();
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"You have already signed-in with [u]{accounts.Count()}[/] accounts");

                    var newSigninOption = "New Sign-in/Interactive Mode...";

                    var accountNames = accounts.Select(app => app.Username!).ToList();
                    accountNames.Add(newSigninOption);

                    var chosenAccount = await _consoleReader.SelectionPromptAsync(
                            "Which [green]account[/] do you want to use?",
                            accountNames,
                            ct: ct);

                    selectedAccount = accounts.FirstOrDefault(app => app.Username == chosenAccount);
                }
            }

            _selectedAccount = selectedAccount;
        }

        public async Task ClearAllCacheAsync()
        {
            await InitAppAsync();

            var accounts = (await _app.GetAccountsAsync()).ToList();

            // clear the cache
            for (int i = 0; i < accounts.Count; i++)
            {
                try
                {
                    await _app.RemoveAsync(accounts[i]);
                }
                catch
                {
                    // Ignore
                }
            }

            var app = PublicClientApplicationBuilder.Create(ClientId)
                .Build();

            accounts = (await app.GetAccountsAsync()).ToList();

            // clear the cache
            while (accounts.Count != 0)
            {
                await app.RemoveAsync(accounts.First());
                accounts = (await app.GetAccountsAsync()).ToList();
            }
        }
    }
}
