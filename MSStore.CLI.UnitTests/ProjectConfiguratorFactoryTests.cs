// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using MSStore.CLI.ProjectConfigurators;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProjectConfiguratorFactoryTests : BaseCommandLineTest
    {
        [TestMethod]
        [DataRow("https://www.microsoft.com", typeof(PWAProjectConfigurator), null)]
        [DataRow("https://store.microsoft.com", typeof(PWAProjectConfigurator), null)]
        [DataRow(null, typeof(FlutterProjectConfigurator), new[] { "FlutterProject" })]
        [DataRow(null, typeof(UWPProjectConfigurator), new[] { "UWPProject" })]
        [DataRow(null, typeof(ElectronProjectConfigurator), new[] { "ElectronProject", "Npm" })]
        [DataRow(null, typeof(ElectronProjectConfigurator), new[] { "ElectronProject", "Yarn" })]
        [DataRow(null, typeof(ReactNativeProjectConfigurator), new[] { "ReactNativeProject", "Npm" })]
        [DataRow(null, typeof(ReactNativeProjectConfigurator), new[] { "ReactNativeProject", "Yarn" })]
        [DataRow(null, typeof(WinUIProjectConfigurator), new[] { "WinUIProject" })]
        [DataRow(null, typeof(MauiProjectConfigurator), new[] { "MauiProject" })]
        public async Task ProjectConfiguratorFactoryFindsURLProperly(string pathOrUrl, Type expectedProjectConfiguratorType, string[] testDataProjectSubPath)
        {
            string? path = null;
            if (testDataProjectSubPath != null && testDataProjectSubPath.Length != 0)
            {
                path = CopyFilesRecursively(Path.Combine(testDataProjectSubPath));

                AssertBasedOnTestDataProjectSubPath(testDataProjectSubPath);

                pathOrUrl = path;
            }

            await RunTestAsync(async (parseResult, host, ct) =>
            {
                var projectConfiguratorFactory = host.Services.GetService<IProjectConfiguratorFactory>()!;

                if (testDataProjectSubPath != null && testDataProjectSubPath.Length != 0 && path != null)
                {
                    SetupBasedOnTestDataProjectSubPath(new DirectoryInfo(path), testDataProjectSubPath);
                }

                var projectConfigurator = await projectConfiguratorFactory.FindProjectConfiguratorAsync(pathOrUrl, ct);

                projectConfigurator.Should().BeOfType(expectedProjectConfiguratorType);
            });
        }
    }
}
