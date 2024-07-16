// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;

namespace MSStore.CLI.Commands.Submission
{
    internal class RolloutCommand : Command
    {
        public RolloutCommand()
            : base("rollout", "Execute rollout related operations")
        {
            AddCommand(new Rollout.GetCommand());
            AddCommand(new Rollout.UpdateCommand());
            AddCommand(new Rollout.HaltCommand());
            AddCommand(new Rollout.FinalizeCommand());
            this.SetDefaultHelpHandler();
        }
    }
}
