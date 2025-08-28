// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;

namespace MSStore.CLI.Commands.Flights
{
    internal class FlightSubmissionCommand : Command
    {
        public FlightSubmissionCommand(Submission.GetCommand getCommand, Submission.DeleteCommand deleteCommand, Submission.UpdateCommand updateCommand, Submission.PublishCommand publishCommand, Submission.PollCommand pollCommand, Submission.StatusCommand statusCommand, Submission.RolloutCommand rolloutCommand)
            : base("submission", "Execute flight submissions related tasks.")
        {
            Subcommands.Add(getCommand);
            Subcommands.Add(deleteCommand);
            Subcommands.Add(updateCommand);
            Subcommands.Add(publishCommand);
            Subcommands.Add(pollCommand);
            Subcommands.Add(statusCommand);
            Subcommands.Add(rolloutCommand);
        }
    }
}
