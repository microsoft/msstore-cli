// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Spectre.Console;

namespace MSStore.CLI.Services.PartnerCenter
{
    internal static class AccountEnrollmentExtensions
    {
        public static void WriteInfo(this AccountEnrollment account, IAnsiConsole ansiConsole)
        {
            ansiConsole.WriteLine($"Developer Account Name: \t{account.Name}");
            ansiConsole.WriteLine($"Developer Account Type: \t{account.AccountType}");
            ansiConsole.WriteLine($"Developer Account Status: \t{account.Status}");

            ansiConsole.WriteLine();
        }
    }
}
