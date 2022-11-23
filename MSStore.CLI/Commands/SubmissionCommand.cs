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
            LanguageOption = new Option<string>(
                aliases: new string[] { "--language", "-l" },
                getDefaultValue: () => "en",
                description: "Select which language you want to retrieve.");
            SkipInitialPolling = new Option<bool>(
                aliases: new string[] { "--skipInitialPolling", "-s" },
                getDefaultValue: () => false,
                description: "Skip the initial polling before executing the action.");
            ProductIdArgument = new Argument<string>(
                name: "productId",
                description: "The product ID.");
        }

        public SubmissionCommand()
            : base("submission", "Executes commands to a store submission.")
        {
            AddCommand(new StatusCommand());
            AddCommand(new GetCommand());
            AddCommand(new GetListingAssetsCommand());
            AddCommand(new UpdateMetadataCommand());
            AddCommand(new UpdateCommand());
            AddCommand(new PollCommand());
            AddCommand(new Submission.PublishCommand());
            AddCommand(new DeleteCommand());
            this.SetDefaultHelpHandler();
        }
    }
}
