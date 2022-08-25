// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands.Apps;

namespace MSStore.CLI.Commands
{
    internal class AppsCommand : Command
    {
        public AppsCommand()
            : base("apps", "Execute apps related tasks.")
        {
            AddCommand(new ListCommand());
            AddCommand(new GetCommand());
            this.SetDefaultHelpHandler();
        }
    }
}
