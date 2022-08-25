// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MSStore.CLI.Commands.Init.Setup;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProjectConfiguratorFactoryTests : BaseCommandLineTest
    {
        [TestMethod]
        [DataRow("https://www.microsoft.com", typeof(PWAProjectConfigurator))]
        [DataRow("https://store.microsoft.com", typeof(PWAProjectConfigurator))]
        public async Task ProjectConfiguratorFactoryFindsURLProperly(string pathOrUrl, Type expectedProjectConfiguratorType)
        {
            await RunTestAsync((context) =>
            {
                var host = context.GetHost();
                var projectConfiguratorFactory = host.Services.GetService<IProjectConfiguratorFactory>()!;

                var projectConfigurator = projectConfiguratorFactory.FindProjectConfigurator(pathOrUrl);

                projectConfigurator.Should().BeOfType(expectedProjectConfiguratorType);

                return Task.CompletedTask;
            });
        }

        [TestMethod]
        public async Task ProjectConfiguratorFactoryFindsFlutterProject()
        {
            await RunTestAsync((context) =>
            {
                var host = context.GetHost();
                var projectConfiguratorFactory = host.Services.GetService<IProjectConfiguratorFactory>()!;

                var projectConfigurator = projectConfiguratorFactory.FindProjectConfigurator("./TestData/FlutterProject/");

                projectConfigurator.Should().BeOfType<FlutterProjectConfigurator>();

                return Task.CompletedTask;
            });
        }

        [TestMethod]
        public async Task ProjectConfiguratorFactoryFindsUWPProject()
        {
            await RunTestAsync((context) =>
            {
                var host = context.GetHost();
                var projectConfiguratorFactory = host.Services.GetService<IProjectConfiguratorFactory>()!;

                var projectConfigurator = projectConfiguratorFactory.FindProjectConfigurator("./TestData/UWPProject/");

                projectConfigurator.Should().BeOfType<UWPProjectConfigurator>();

                return Task.CompletedTask;
            });
        }
    }
}
