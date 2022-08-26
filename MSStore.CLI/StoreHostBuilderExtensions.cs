// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine.Hosting;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using MSStore.CLI.Commands;

namespace MSStore.CLI
{
    internal static class StoreHostBuilderExtensions
    {
        // IL Trimming, until System.CommandLine.Hosting supports it
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InitCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InfoCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReconfigureCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SettingsCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Apps.ListCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Apps.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.StatusCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.GetListingAssetsCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.UpdateMetadataCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.UpdateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.PollCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.PublishCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MicrosoftStoreCLI.Handler))]
        public static IHostBuilder ConfigureStoreCLICommands(this IHostBuilder builder)
        {
            return builder
                  .UseCommandHandler<InitCommand, InitCommand.Handler>()
                  .UseCommandHandler<InfoCommand, InfoCommand.Handler>()
                  .UseCommandHandler<ReconfigureCommand, ReconfigureCommand.Handler>()
                  .UseCommandHandler<SettingsCommand, SettingsCommand.Handler>()
                  .UseCommandHandler<Commands.Apps.ListCommand, Commands.Apps.ListCommand.Handler>()
                  .UseCommandHandler<Commands.Apps.GetCommand, Commands.Apps.GetCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.StatusCommand, Commands.Submission.StatusCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.GetCommand, Commands.Submission.GetCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.GetListingAssetsCommand, Commands.Submission.GetListingAssetsCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.UpdateMetadataCommand, Commands.Submission.UpdateMetadataCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.UpdateCommand, Commands.Submission.UpdateCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.PollCommand, Commands.Submission.PollCommand.Handler>()
                  .UseCommandHandler<Commands.Submission.PublishCommand, Commands.Submission.PublishCommand.Handler>()
                  .UseCommandHandler<MicrosoftStoreCLI, MicrosoftStoreCLI.Handler>();
        }
    }
}
