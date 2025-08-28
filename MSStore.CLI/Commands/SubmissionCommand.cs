// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using MSStore.CLI.Commands.Submission;

namespace MSStore.CLI.Commands
{
    internal class SubmissionCommand : Command
    {
        internal static readonly Option<string> LanguageOption;
        internal static readonly Option<bool> SkipInitialPolling;
        internal static readonly Argument<string> ProductIdArgument;

        static SubmissionCommand()
        {
            LanguageOption = new Option<string>("--language", "-l")
            {
                DefaultValueFactory = _ => "en",
                Description = "Select which language you want to retrieve."
            };

            SkipInitialPolling = new Option<bool>("--skipInitialPolling", "-s")
            {
                DefaultValueFactory = _ => false,
                Description = "Skip the initial polling before executing the action."
            };
            ProductIdArgument = new Argument<string>("productId")
            {
                Description = "The product ID."
            };
        }

        public SubmissionCommand(StatusCommand statusCommand, GetCommand getCommand, GetListingAssetsCommand getListingAssetsCommand, UpdateMetadataCommand updateMetadataCommand, UpdateCommand updateCommand, PollCommand pollCommand, Submission.PublishCommand publishCommand, DeleteCommand deleteCommand, RolloutCommand rolloutCommand)
            : base("submission", "Executes commands to a store submission.")
        {
            Subcommands.Add(statusCommand);
            Subcommands.Add(getCommand);
            Subcommands.Add(getListingAssetsCommand);
            Subcommands.Add(updateMetadataCommand);
            Subcommands.Add(updateCommand);
            Subcommands.Add(pollCommand);
            Subcommands.Add(publishCommand);
            Subcommands.Add(deleteCommand);
            Subcommands.Add(rolloutCommand);
        }
    }
}
