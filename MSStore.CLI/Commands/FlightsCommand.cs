// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands.Flights;

namespace MSStore.CLI.Commands
{
    internal class FlightsCommand : Command
    {
        public FlightsCommand()
            : base("flights", "Execute flights related tasks.")
        {
            AddCommand(new ListCommand());
            AddCommand(new GetCommand());
            AddCommand(new FlightSubmissionCommand());
            this.SetDefaultHelpHandler();
        }
    }
}
