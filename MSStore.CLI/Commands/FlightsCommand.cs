// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands.Flights;

namespace MSStore.CLI.Commands
{
    internal class FlightsCommand : Command
    {
        public FlightsCommand(ListCommand listCommand, GetCommand getCommand, DeleteCommand deleteCommand, CreateCommand createCommand, FlightSubmissionCommand flightSubmissionCommand)
            : base("flights", "Execute flights related tasks.")
        {
            Subcommands.Add(listCommand);
            Subcommands.Add(getCommand);
            Subcommands.Add(deleteCommand);
            Subcommands.Add(createCommand);
            Subcommands.Add(flightSubmissionCommand);
        }
    }
}
