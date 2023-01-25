// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MSStore.CLI.ProjectConfigurators;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ProjectConfiguratorFactoryTests : BaseCommandLineTest
    {
        [TestMethod]
        [DataRow("https://www.microsoft.com", typeof(PWAProjectConfigurator))]
        [DataRow("https://store.microsoft.com", typeof(PWAProjectConfigurator))]
        [DataRow(null, typeof(FlutterProjectConfigurator), "FlutterProject")]
        [DataRow(null, typeof(UWPProjectConfigurator), "UWPProject")]
        [DataRow(null, typeof(ElectronProjectConfigurator), "ElectronProject", "Npm")]
        [DataRow(null, typeof(ElectronProjectConfigurator), "ElectronProject", "Yarn")]
        [DataRow(null, typeof(ReactNativeProjectConfigurator), "ReactNativeProject", "Npm")]
        [DataRow(null, typeof(ReactNativeProjectConfigurator), "ReactNativeProject", "Yarn")]
        [DataRow(null, typeof(WinUIProjectConfigurator), "WinUIProject")]
        public async Task ProjectConfiguratorFactoryFindsURLProperly(string pathOrUrl, Type expectedProjectConfiguratorType, params string[] testDataProjectSubPath)
        {
            await RunTestAsync(async (context) =>
            {
                var ct = context.GetCancellationToken();

                var host = context.GetHost();
                var projectConfiguratorFactory = host.Services.GetService<IProjectConfiguratorFactory>()!;

                if (testDataProjectSubPath != null && testDataProjectSubPath.Any())
                {
                    var list = testDataProjectSubPath.ToList();
                    list.Insert(0, ".");
                    list.Insert(1, "TestData");
                    testDataProjectSubPath = list.ToArray();
                    var path = Path.Combine(testDataProjectSubPath);
                    var dirInfo = new DirectoryInfo(path);

                    if (testDataProjectSubPath.Contains("UWPProject"))
                    {
                        DefaultMSBuildExecution(dirInfo);
                    }
                    else if(testDataProjectSubPath.Contains("WinUIProject"))
                    {
                        DefaultMSBuildExecution(dirInfo);
                        SetupWinUI(dirInfo);
                    }
                    else if (testDataProjectSubPath.Contains("ReactNativeProject"))
                    {
                        if (testDataProjectSubPath.Contains("Npm"))
                        {
                            SetupNpmListReactNative(dirInfo, true);
                            SetupNpmInstall(dirInfo);
                        }
                        else if (testDataProjectSubPath.Contains("Yarn"))
                        {
                            SetupYarnListReactNative(dirInfo, true);
                            SetupYarnInstall(dirInfo);
                        }
                    }
                    else if (testDataProjectSubPath.Contains("ElectronProject"))
                    {
                        if (testDataProjectSubPath.Contains("Npm"))
                        {
                            SetupNpmListReactNative(dirInfo, false);
                            SetupNpmInstall(dirInfo);
                        }
                        else if (testDataProjectSubPath.Contains("Yarn"))
                        {
                            SetupYarnListReactNative(dirInfo, false);
                            SetupYarnInstall(dirInfo);
                        }
                    }

                    pathOrUrl = path;
                }

                var projectConfigurator = await projectConfiguratorFactory.FindProjectConfiguratorAsync(pathOrUrl, ct);

                projectConfigurator.Should().BeOfType(expectedProjectConfiguratorType);
            });
        }
    }
}
