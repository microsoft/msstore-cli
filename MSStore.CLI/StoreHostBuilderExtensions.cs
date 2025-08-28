// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MSStore.CLI.Commands;

namespace MSStore.CLI
{
    internal static class StoreHostBuilderExtensions
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InitCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InfoCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ReconfigureCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SettingsCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Settings.SetPublisherDisplayNameCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Apps.ListCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Apps.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.StatusCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.GetListingAssetsCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.UpdateMetadataCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.UpdateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.PollCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.PublishCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.DeleteCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.Rollout.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.Rollout.UpdateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.Rollout.HaltCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Submission.Rollout.FinalizeCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.ListCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.DeleteCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.CreateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.DeleteCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.UpdateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.PublishCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.PollCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.StatusCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.Rollout.GetCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.Rollout.UpdateCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.Rollout.HaltCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Commands.Flights.Submission.Rollout.FinalizeCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PackageCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PublishCommand.Handler))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MicrosoftStoreCLI.Handler))]
        public static IHostBuilder ConfigureStoreCLICommands(this IHostBuilder builder)
        {
            return builder
                .ConfigureServices(services =>
                {
                    services
                        .UseCommandHandler<InitCommand, InitCommand.Handler>()
                        .UseCommandHandler<InfoCommand, InfoCommand.Handler>()
                        .UseCommandHandler<ReconfigureCommand, ReconfigureCommand.Handler>()
                        .UseCommandHandler<SettingsCommand, SettingsCommand.Handler>()
                        .UseCommandHandler<Commands.Settings.SetPublisherDisplayNameCommand, Commands.Settings.SetPublisherDisplayNameCommand.Handler>()
                        .UseCommandHandler<PackageCommand, PackageCommand.Handler>()
                        .UseCommandHandler<PublishCommand, PublishCommand.Handler>()
                        .ConfigureCommand<AppsCommand>()
                        .UseCommandHandler<Commands.Apps.ListCommand, Commands.Apps.ListCommand.Handler>()
                        .UseCommandHandler<Commands.Apps.GetCommand, Commands.Apps.GetCommand.Handler>()
                        .ConfigureCommand<SubmissionCommand>()
                        .UseCommandHandler<Commands.Submission.StatusCommand, Commands.Submission.StatusCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.GetCommand, Commands.Submission.GetCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.GetListingAssetsCommand, Commands.Submission.GetListingAssetsCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.UpdateMetadataCommand, Commands.Submission.UpdateMetadataCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.UpdateCommand, Commands.Submission.UpdateCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.PollCommand, Commands.Submission.PollCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.PublishCommand, Commands.Submission.PublishCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.DeleteCommand, Commands.Submission.DeleteCommand.Handler>()
                        .ConfigureCommand<Commands.Submission.RolloutCommand>()
                        .UseCommandHandler<Commands.Submission.Rollout.GetCommand, Commands.Submission.Rollout.GetCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.Rollout.UpdateCommand, Commands.Submission.Rollout.UpdateCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.Rollout.HaltCommand, Commands.Submission.Rollout.HaltCommand.Handler>()
                        .UseCommandHandler<Commands.Submission.Rollout.FinalizeCommand, Commands.Submission.Rollout.FinalizeCommand.Handler>()
                        .ConfigureCommand<FlightsCommand>()
                        .UseCommandHandler<Commands.Flights.ListCommand, Commands.Flights.ListCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.GetCommand, Commands.Flights.GetCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.DeleteCommand, Commands.Flights.DeleteCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.CreateCommand, Commands.Flights.CreateCommand.Handler>()
                        .ConfigureCommand<Commands.Flights.FlightSubmissionCommand>()
                        .UseCommandHandler<Commands.Flights.Submission.GetCommand, Commands.Flights.Submission.GetCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.DeleteCommand, Commands.Flights.Submission.DeleteCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.UpdateCommand, Commands.Flights.Submission.UpdateCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.PublishCommand, Commands.Flights.Submission.PublishCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.PollCommand, Commands.Flights.Submission.PollCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.StatusCommand, Commands.Flights.Submission.StatusCommand.Handler>()
                        .ConfigureCommand<Commands.Flights.Submission.RolloutCommand>()
                        .UseCommandHandler<Commands.Flights.Submission.Rollout.GetCommand, Commands.Flights.Submission.Rollout.GetCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.Rollout.UpdateCommand, Commands.Flights.Submission.Rollout.UpdateCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.Rollout.HaltCommand, Commands.Flights.Submission.Rollout.HaltCommand.Handler>()
                        .UseCommandHandler<Commands.Flights.Submission.Rollout.FinalizeCommand, Commands.Flights.Submission.Rollout.FinalizeCommand.Handler>()
                        .UseCommandHandler<MicrosoftStoreCLI, MicrosoftStoreCLI.Handler>();
                });
        }

        public static IServiceCollection UseCommandHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
            where TCommand : Command
            where THandler : AsynchronousCommandLineAction
        {
            return services
                .AddSingleton<THandler>()
                .AddSingleton<TCommand>(sp =>
                {
                    var command = ActivatorUtilities.CreateInstance<TCommand>(sp);
                    command.Options.Add(MicrosoftStoreCLI.VerboseOption);
                    command.SetAction((parseResult, ct) => sp.GetRequiredService<THandler>().InvokeAsync(parseResult, ct));
                    return command;
                });
        }

        public static IServiceCollection ConfigureCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this IServiceCollection services)
            where TCommand : Command
        {
            return services
                .AddSingleton<TCommand>(sp =>
                {
                    var command = ActivatorUtilities.CreateInstance<TCommand>(sp);
                    command.Options.Add(MicrosoftStoreCLI.VerboseOption);
                    return command;
                });
        }
    }
}
