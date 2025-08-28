// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands.Apps;

namespace MSStore.CLI.Commands
{
    internal class AppsCommand : Command
    {
        public AppsCommand(ListCommand listCommand, GetCommand getCommand)
            : base("apps", "Execute apps related tasks.")
        {
            Subcommands.Add(listCommand);
            Subcommands.Add(getCommand);
        }
    }
}
