// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;

namespace MSStore.CLI.Helpers
{
    internal static class ParseResultExtensions
    {
        public static bool IsVerbose(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Command is MicrosoftStoreCLI storeCLI &&
                    parseResult.GetValue(MicrosoftStoreCLI.VerboseOption);
        }
    }
}
