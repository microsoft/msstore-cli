// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine.Parsing;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class ParseResultExtensions
    {
        public static bool IsVerbose(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Command is MicrosoftStoreCLI storeCLI &&
                    parseResult.GetValueForOption(storeCLI.VerboseOption);
        }

        public static IAnsiConsole StdOut(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Command is MicrosoftStoreCLI storeCLI ?
                storeCLI.StdOut :
                AnsiConsole.Console;
        }

        public static IAnsiConsole StdErr(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Command is MicrosoftStoreCLI storeCLI ?
                storeCLI.StdErr :
                AnsiConsole.Console;
        }
    }
}
