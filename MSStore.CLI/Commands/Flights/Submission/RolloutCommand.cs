// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;

namespace MSStore.CLI.Commands.Flights.Submission
{
    internal class RolloutCommand : Command
    {
        public RolloutCommand(Rollout.GetCommand getCommand, Rollout.UpdateCommand updateCommand, Rollout.HaltCommand haltCommand, Rollout.FinalizeCommand finalizeCommand)
            : base("rollout", "Execute flight rollout related operations")
        {
            Subcommands.Add(getCommand);
            Subcommands.Add(updateCommand);
            Subcommands.Add(haltCommand);
            Subcommands.Add(finalizeCommand);
        }
    }
}
