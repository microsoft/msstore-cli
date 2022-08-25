// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Spectre.Console;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal static class AccountEnrollmentExtensions
    {
        public static void WriteInfo(this AccountEnrollment account)
        {
            AnsiConsole.WriteLine($"Developer Account Name: \t{account.Name}");
            AnsiConsole.WriteLine($"Developer Account Type: \t{account.AccountType}");
            AnsiConsole.WriteLine($"Developer Account Status: \t{account.Status}");

            AnsiConsole.WriteLine();
        }
    }
}
