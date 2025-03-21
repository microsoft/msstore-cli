// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Commands;
using MSStore.CLI.Helpers;
using MSStore.CLI.ProjectConfigurators;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.ElectronManager;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.PWABuilder;
using MSStore.CLI.Services.Telemetry;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI.UnitTests
{
    public class BaseCommandLineTest
    {
        internal MicrosoftStoreCLI Cli { get; private set; } = null!;
        internal Mock<IConsoleReader> FakeConsole { get; private set; } = null!;
        internal Mock<IConfigurationManager<Configurations>> FakeConfigurationManager { get; private set; } = null!;
        internal Mock<IConfigurationManager<TelemetryConfigurations>> FakeTelemetryConfigurationManager { get; private set; } = null!;
        internal Mock<IPartnerCenterManager> PartnerCenterManager { get; private set; } = null!;
        internal Mock<IBrowserLauncher> BrowserLauncher { get; private set; } = null!;
        internal Mock<ICredentialManager> CredentialManager { get; private set; } = null!;
        internal Mock<IPWABuilderClient> PWABuilderClient { get; private set; } = null!;
        internal Mock<IGraphClient> GraphClient { get; private set; } = null!;
        internal Mock<ITokenManager> TokenManager { get; private set; } = null!;
        internal Mock<IPWAAppInfoManager> PWAAppInfoManager { get; private set; } = null!;
        internal Mock<ElectronManifestManager> ElectronManifestManager { get; private set; } = null!;
        internal Mock<AppXManifestManager> AppXManifestManager { get; private set; } = null!;
        internal Mock<INuGetPackageManager> NuGetPackageManager { get; private set; } = null!;
        internal Mock<IZipFileManager> ZipFileManager { get; private set; } = null!;
        internal Mock<IEnvironmentInformationService> EnvironmentInformationService { get; private set; } = null!;
        internal List<string> UserNames { get; } = [];
        internal List<string> Secrets { get; } = [];

        internal Mock<IExternalCommandExecutor> ExternalCommandExecutor { get; private set; } = null!;
        internal Mock<IStoreAPIFactory> FakeStoreAPIFactory { get; private set; } = null!;
        internal Mock<IStoreAPI> FakeStoreAPI { get; private set; } = null!;
        internal Mock<IStorePackagedAPI> FakeStorePackagedAPI { get; private set; } = null!;

        protected List<DevCenterApplication> FakeApps { get; } =
            [
                new DevCenterApplication
                {
                    Id = "9PN3ABCDEFGA",
                    PrimaryName = "Fake App 1"
                },
                new DevCenterApplication
                {
                    Id = "9PN3ABCDEFGB",
                    PrimaryName = "Fake App 2"
                },
                new DevCenterApplication
                {
                    Id = "9PN3ABCDEFGC",
                    PrimaryName = "Fake App 3"
                }
            ];

        protected List<DevCenterFlight> FakeFlights { get; } =
            [
                new DevCenterFlight
                {
                    FlightId = "632B6A77-0E18-4B41-9033-3614D2174F2C",
                    FriendlyName = "FakeFlight1"
                },
                new DevCenterFlight
                {
                    FlightId = "632B6A77-0E18-4B41-9033-3614D2174F2D",
                    FriendlyName = "FakeFlight2"
                }
            ];

        internal static Organization DefaultOrganization { get; } = new Organization
        {
            Id = new Guid("F3C1CCB6-09C0-4BAB-BABA-C034BFB60EF9")
        };

        private Parser _parser = null!;
        protected IAnsiConsole ErrorAnsiConsole { get; private set; } = null!;

        protected static string CopyFilesRecursively(string sourcePath, [CallerMemberName] string caller = null!)
        {
            sourcePath = Path.Combine("TestData", sourcePath);

            var targetPath = Path.Combine(caller, sourcePath);

            Directory.CreateDirectory(targetPath);

            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath).Replace(".template", string.Empty), true);
            }

            return targetPath;
        }

        protected static void AssertBasedOnTestDataProjectSubPath(string[] testDataProjectSubPath)
        {
            if (testDataProjectSubPath.Contains("UWPProject") || testDataProjectSubPath.Contains("WinUIProject") || testDataProjectSubPath.Contains("MauiProject"))
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Assert.Inconclusive("This test is only valid on non-Windows platforms");
                }
            }
        }

        [TestInitialize]
        public void Initialize()
        {
            BrowserLauncher = new Mock<IBrowserLauncher>();
            PartnerCenterManager = new Mock<IPartnerCenterManager>();
            PartnerCenterManager
                .Setup(x => x.GetEnrollmentAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountEnrollments
                {
                    Items = []
                });
            PartnerCenterManager
                .Setup(x => x.Enabled)
                .Returns(true);

            FakeConfigurationManager = new Mock<IConfigurationManager<Configurations>>();
            FakeConfigurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Configurations());

            FakeTelemetryConfigurationManager = new Mock<IConfigurationManager<TelemetryConfigurations>>();
            FakeTelemetryConfigurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TelemetryConfigurations());

            CredentialManager = new Mock<ICredentialManager>();
            CredentialManager
                .Setup(x => x.WriteCredential(Capture.In(UserNames), Capture.In(Secrets)));
            CredentialManager
                .Setup(x => x.ClearCredentials(It.IsAny<string>()))
                .Callback(() =>
                {
                    UserNames.Clear();
                    Secrets.Clear();
                });
            CredentialManager
                .Setup(x => x.ReadCredential(It.IsAny<string>()))
                .Returns((string userName) =>
                {
                    return userName.Equals(UserNames.Last(), StringComparison.OrdinalIgnoreCase) ? Secrets.Last() : string.Empty;
                });
            ExternalCommandExecutor = new Mock<IExternalCommandExecutor>();
            FakeConsole = new Mock<IConsoleReader>();
            FakeConsole
                .Setup(x => x.SelectionPromptAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string title, IEnumerable<string> choices, int pageSize, Func<string, string> displaySelector, CancellationToken ct) =>
                {
                    return choices.First();
                });

            PWABuilderClient = new Mock<IPWABuilderClient>();
            GraphClient = new Mock<IGraphClient>();
            GraphClient
                .Setup(x => x.Enabled)
                .Returns(true);

            FakeStoreAPI = new Mock<IStoreAPI>();
            FakeStorePackagedAPI = new Mock<IStorePackagedAPI>();

            FakeStoreAPIFactory = new Mock<IStoreAPIFactory>();
            FakeStoreAPIFactory
                .Setup(fac => fac.CreateAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeStoreAPI.Object);
            FakeStoreAPIFactory
                .Setup(fac => fac.CreatePackagedAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeStorePackagedAPI.Object);

            Cli = [];

            StorePackagedAPI.DefaultSubmissionPollDelay = TimeSpan.Zero;

            Cli.AddCommand(new TestCommand(this));

            var azureBlobManagerMock = new Mock<IAzureBlobManager>();
            azureBlobManagerMock
                .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            var fileDownloader = new Mock<IFileDownloader>();
            fileDownloader
                .Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<ILogger?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var imageConverter = new Mock<IImageConverter>();
            imageConverter
                .Setup(x => x.ConvertIcoToPngAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ElectronManifestManager = new Mock<ElectronManifestManager> { CallBase = true };

            RefreshAnsiConsole();

            NuGetPackageManager = new Mock<INuGetPackageManager>();

            PWAAppInfoManager = new Mock<IPWAAppInfoManager>();

            ZipFileManager = new Mock<IZipFileManager>();

            EnvironmentInformationService = new Mock<IEnvironmentInformationService>();
            EnvironmentInformationService
                .Setup(x => x.IsRunningOnCI)
                .Returns(false);

            TokenManager = new Mock<ITokenManager>();

            var builder = new CommandLineBuilder(Cli);
            _parser = builder.UseHost(_ => Host.CreateDefaultBuilder(null), (builder) => builder
                .UseEnvironment("CLI")
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddSingleton(FakeConfigurationManager.Object)
                        .AddSingleton(FakeTelemetryConfigurationManager.Object)
                        .AddSingleton(BrowserLauncher.Object)
                        .AddSingleton(CredentialManager.Object)
                        .AddSingleton(FakeConsole.Object)
                        .AddSingleton<IAnsiConsole>((sp) => ErrorAnsiConsole)
                        .AddSingleton(ExternalCommandExecutor.Object)
                        .AddSingleton<IProjectConfiguratorFactory, ProjectConfiguratorFactory>()
                        .AddSingleton(new TelemetryClient(new TelemetryConfiguration()))
                        .AddScoped<IProjectConfigurator, FlutterProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, UWPProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, WinUIProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, PWAProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, ElectronProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, ReactNativeProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, MauiProjectConfigurator>()
                        .AddScoped<IProjectPublisher, MSIXProjectPublisher>()
                        .AddScoped<ICLIConfigurator, CLIConfigurator>()
                        .AddSingleton(FakeStoreAPIFactory.Object)
                        .AddScoped(sp => PWABuilderClient.Object)
                        .AddScoped(sp => azureBlobManagerMock.Object)
                        .AddScoped(sp => GraphClient.Object)
                        .AddScoped(sp => PartnerCenterManager.Object)
                        .AddScoped(sp => ZipFileManager.Object)
                        .AddScoped(sp => EnvironmentInformationService.Object)
                        .AddScoped(sp => TokenManager.Object)
                        .AddScoped(sp => fileDownloader.Object)
                        .AddScoped(sp => imageConverter.Object)
                        .AddScoped(sp => PWAAppInfoManager.Object)
                        .AddScoped<IElectronManifestManager>(sp => ElectronManifestManager.Object)
                        .AddScoped(sp => NuGetPackageManager.Object)
                        .AddScoped<IAppXManifestManager>(sp => AppXManifestManager.Object)
                        .AddSingleton(Cli);

                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddProvider(new CustomSpectreConsoleLoggerProvider(ErrorAnsiConsole));
                    });
                })
                .ConfigureStoreCLICommands()
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                }))
                .AddMiddleware(
                    async (context, next) =>
                    {
                        var ct = context.GetCancellationToken();

                        var host = context.GetHost();

                        var configurationManager = host.Services.GetService<IConfigurationManager<Configurations>>()!;
                        var credentialManager = host.Services.GetService<ICredentialManager>()!;
                        var consoleReader = host.Services.GetService<IConsoleReader>()!;
                        var cliConfigurator = host.Services.GetService<ICLIConfigurator>()!;
                        var logger = host.Services.GetService<ILogger<Program>>()!;

                        if (context.ParseResult.CommandResult.Command is MicrosoftStoreCLI
                            || context.ParseResult.CommandResult.Command is ReconfigureCommand
                            || context.ParseResult.CommandResult.Command is TestCommand
                            || await MicrosoftStoreCLI.InitAsync(ErrorAnsiConsole, configurationManager, credentialManager, consoleReader, cliConfigurator, logger, ct))
                        {
                            await next(context).ConfigureAwait(false);
                        }
                    }, MiddlewareOrder.Default)
                .UseVersionOption()
                .UseEnvironmentVariableDirective()
                .UseParseDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseTypoCorrections()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .UseHelp()
                .CancelOnProcessTermination()
                .Build();
        }

        protected void FakeLogin(string? publisherDisplayName = null)
        {
            FakeConfigurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Configurations
                {
                    SellerId = 1,
                    TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
                    ClientId = new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45"),
                    PublisherDisplayName = publisherDisplayName
                });
            UserNames.Add("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45");
            Secrets.Add("testSecret");
        }

        protected void FakeLoginWithCert(string? publisherDisplayName = null)
        {
            FakeConfigurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Configurations
                {
                    SellerId = 1,
                    TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
                    ClientId = new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45"),
                    CertificateThumbprint = "abc",
                    PublisherDisplayName = publisherDisplayName
                });
            UserNames.Add("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45");
            Secrets.Add(string.Empty);
        }

        internal void AddDefaultFakeAccount()
        {
            AddFakeAccount(new AccountEnrollment
            {
                Id = "12345",
                Name = "PublisherName",
                AccountType = "individual",
                Status = "active"
            });
        }

        internal void AddDefaultGraphOrg()
        {
            var mockAccount = new Mock<Microsoft.Identity.Client.IAccount>();
            mockAccount
                .Setup(a => a.Username)
                .Returns("testUserName@fakedomain.com");
            mockAccount
                .Setup(a => a.HomeAccountId)
                .Returns(new Microsoft.Identity.Client.AccountId("id", "123", DefaultOrganization.Id.ToString()));
            TokenManager
                .Setup(x => x.CurrentUser)
                .Returns((Microsoft.Identity.Client.IAccount?)null);
            TokenManager
                .Setup(x => x.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Microsoft.Identity.Client.AuthenticationResult?)null);
            TokenManager
                .Setup(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    TokenManager
                        .Setup(x => x.CurrentUser)
                        .Returns(mockAccount.Object);
                });
        }

        internal void AddFakeAccount(AccountEnrollment? accountEnrollment)
        {
            var items = new List<AccountEnrollment>();

            if (accountEnrollment != null)
            {
                items.Add(accountEnrollment);
            }

            PartnerCenterManager
                .Setup(x => x.GetEnrollmentAccountsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccountEnrollments
                {
                    TotalCount = 1,
                    Items = items
                });
        }

        protected void AddDefaultFakeSubmission()
        {
            var fakeSubmission = new DevCenterSubmission
            {
                Id = "123456789",
                ApplicationCategory = DevCenterApplicationCategory.NotSet,
                FileUploadUrl = "https://azureblob.com/fileupload",
                ApplicationPackages =
                    [
                        new ApplicationPackage
                        {
                            Id = "123456789",
                            Version = "1.0.0",
                        }
                    ],
                StatusDetails = new StatusDetails
                {
                    Warnings =
                        [
                            new CodeAndDetail
                            {
                                Code = "Code1",
                                Details = "Detail1"
                            }
                        ]
                },
                Listings = new Dictionary<string, DevCenterListing>
                    {
                        {
                            "en-us",
                            new DevCenterListing
                            {
                                BaseListing = new BaseListing
                                {
                                    Description = "BaseListingDescription"
                                }
                            }
                        }
                    }
            };

            FakeStorePackagedAPI
                .Setup(x => x.CreateSubmissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
            FakeStorePackagedAPI
                .Setup(x => x.GetSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
            FakeStorePackagedAPI
                .Setup(x => x.UpdateSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DevCenterSubmission>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
        }

        protected void AddDefaultFakeFlightSubmission()
        {
            var fakeSubmission = new DevCenterFlightSubmission
            {
                Id = "123456789",
                FileUploadUrl = "https://azureblob.com/fileupload",
                FlightPackages =
                    [
                        new ApplicationPackage
                        {
                            Id = "123456789",
                            Version = "1.0.0",
                        }
                    ],
                StatusDetails = new StatusDetails
                {
                    Warnings =
                        [
                            new CodeAndDetail
                            {
                                Code = "Code1",
                                Details = "Detail1"
                            }
                        ]
                }
            };

            FakeStorePackagedAPI
                .Setup(x => x.CreateFlightSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
            FakeStorePackagedAPI
                .Setup(x => x.GetFlightSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
            FakeStorePackagedAPI
                .Setup(x => x.UpdateFlightSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DevCenterFlightSubmissionUpdate>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeSubmission);
        }

        protected void AddFakeApps()
        {
            FakeStorePackagedAPI
                .Setup(x => x.GetApplicationsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeApps);
            FakeStorePackagedAPI
                .Setup(x => x.GetApplicationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string productId, CancellationToken ct) => FakeApps.First(a => a.Id == productId));
        }

        protected void AddFakeFlights()
        {
            FakeStorePackagedAPI
                .Setup(x => x.GetFlightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeFlights);
            FakeStorePackagedAPI
                .Setup(x => x.GetFlightAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string productId, string flightId, CancellationToken ct) => FakeFlights.First(f => f.FlightId == flightId));
            FakeStorePackagedAPI
                .Setup(x => x.DeleteFlightAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string productId, string flightId, CancellationToken ct) =>
                {
                    return null;
                });

            var fakeFlight = new DevCenterFlight
            {
                FlightId = "632B6A77-0E18-4B41-9033-3614D2174F2E",
                FriendlyName = "NewFlight",
                GroupIds = ["1"]
            };

            FakeStorePackagedAPI
                .Setup(x => x.CreateFlightAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeFlight);
        }

        internal void InitDefaultSubmissionStatusResponseQueue()
        {
            FakeStorePackagedAPI
                .SetupSequence(x => x.GetSubmissionStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "CommitStarted"
                })
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "CommitStarted"
                })
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "Published"
                });
        }

        internal void InitDefaultFlightSubmissionStatusResponseQueue()
        {
            FakeStorePackagedAPI
                .SetupSequence(x => x.GetFlightSubmissionStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "CommitStarted"
                })
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "CommitStarted"
                })
                .ReturnsAsync(new DevCenterSubmissionStatusResponse
                {
                    Status = "Published"
                });
        }

        protected void AddDefaultFakeSuccessfulSubmission()
        {
            AddDefaultFakeSubmission();
            InitDefaultSubmissionStatusResponseQueue();

            FakeStorePackagedAPI
                .Setup(x => x.CommitSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterCommitResponse
                {
                    Status = "CommitStarted",
                });
        }

        protected void AddDefaultFakeSuccessfulFlightSubmission()
        {
            AddDefaultFakeFlightSubmission();
            InitDefaultFlightSubmissionStatusResponseQueue();

            FakeStorePackagedAPI
                .Setup(x => x.CommitFlightSubmissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevCenterCommitResponse
                {
                    Status = "CommitStarted",
                });
        }

        protected void SetupNpmInstall(DirectoryInfo dirInfo)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "install"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        protected void SetupYarnInstall(DirectoryInfo dirInfo)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "install"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        protected void SetupNpmListReactNative(DirectoryInfo dirInfo, bool installed)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "npm"),
                    It.Is<string>(s => s == "list react-native"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = installed ? "`-- react-native@" : "`-- (empty)",
                    StdErr = string.Empty
                });
        }

        protected void SetupYarnListReactNative(DirectoryInfo dirInfo, bool installed)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s == "yarn"),
                    It.Is<string>(s => s == "why react-native"),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = installed ? "─ react-native@0.70.0" : "Done in 0s.",
                    StdErr = string.Empty
                });
        }

        protected void DefaultMSBuildExecution(DirectoryInfo dirInfo)
        {
            UWPProjectConfigurator.ResetMSBuildPath();

            ExternalCommandExecutor
                        .Setup(x => x.RunAsync(
                            It.Is<string>(s =>
                                s.Contains("vswhere.exe")),
                            It.Is<string>(s =>
                                s.Contains("-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe")),
                            It.Is<string>(s => s == dirInfo.FullName),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ExternalCommandExecutionResult
                        {
                            ExitCode = 0,
                            StdOut = "MSBuild.exe",
                            StdErr = string.Empty
                        });

            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s.Contains("\"MSBuild.exe\"")),
                    It.Is<string>(s => s.Contains("/t:restore")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        protected void DefaultDotnetRestoreExecution(DirectoryInfo dirInfo)
        {
            ExternalCommandExecutor
                .Setup(x => x.RunAsync(
                    It.Is<string>(s => s.Contains("dotnet")),
                    It.Is<string>(s => s.Contains("restore")),
                    It.Is<string>(s => s == dirInfo.FullName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalCommandExecutionResult
                {
                    ExitCode = 0,
                    StdOut = string.Empty,
                    StdErr = string.Empty
                });
        }

        protected void SetupWinUI(DirectoryInfo dirInfo)
        {
            NuGetPackageManager
                .Setup(x => x.IsPackageInstalledAsync(
                    It.Is<DirectoryInfo>(d => d.FullName == dirInfo.FullName),
                    It.Is<string>(s => s == "Microsoft.WindowsAppSDK"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        protected void SetupMaui(FileInfo fileInfo)
        {
            NuGetPackageManager
                .Setup(x => x.IsMaui(
                    It.Is<FileInfo>(f => f.FullName == fileInfo.FullName)))
                .Returns(true);
        }

        protected void SetupBasedOnTestDataProjectSubPath(DirectoryInfo dirInfo, string[] testDataProjectSubPath)
        {
            if (testDataProjectSubPath.Contains("UWPProject"))
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Assert.Inconclusive("This test is only valid on non-Windows platforms");
                }

                DefaultMSBuildExecution(dirInfo);
            }
            else if (testDataProjectSubPath.Contains("WinUIProject"))
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Assert.Inconclusive("This test is only valid on non-Windows platforms");
                }

                DefaultMSBuildExecution(dirInfo);
                SetupWinUI(dirInfo);
            }
            else if (testDataProjectSubPath.Contains("MauiProject"))
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Assert.Inconclusive("This test is only valid on non-Windows platforms");
                }

                DefaultMSBuildExecution(dirInfo);
                SetupWinUI(dirInfo);
                SetupMaui(dirInfo.GetFiles("*.csproj").First());
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
        }

        protected Task<(string Output, string Error)> RunTestAsync(Func<InvocationContext, Task>? testCallback)
        {
            _testCallback = testCallback;

            return ParseAndInvokeAsync(["test"]);
        }

        protected async Task<(string Output, string Error)> ParseAndInvokeAsync(string[] args, int? expectedResult = 0)
        {
            var outputCapture = new OutputCapture(Console.Out);
            var errorCapture = RefreshAnsiConsole();

            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Yes,
                ColorSystem = ColorSystemSupport.TrueColor,
                Interactive = InteractionSupport.No,
                Out = new CustomAnsiConsoleOutput(outputCapture),
                Enrichment = new ProfileEnrichment
                {
                    UseDefaultEnrichers = false
                }
            });
            AnsiConsole.Profile.Capabilities.Ansi = true;
            AnsiConsole.Profile.Capabilities.Unicode = true;

            var parseResult = _parser.Parse(args);

            if (parseResult.Errors.Any())
            {
                throw new ArgumentException(string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)));
            }

            var testOutputConsole = new TestConsole(outputCapture, errorCapture);

            var invokeTask = parseResult.InvokeAsync(testOutputConsole);

            var result = await invokeTask.ConfigureAwait(false);

            if (expectedResult.HasValue)
            {
                result.Should().Be(expectedResult.Value);
            }
            else
            {
                outputCapture.Captured.ToString().Should().NotContain("💥");
            }

            return (Output: outputCapture.Captured.ToString() ?? string.Empty, Error: errorCapture.Captured.ToString() ?? string.Empty);
        }

        private OutputCapture RefreshAnsiConsole()
        {
            var errorCapture = new OutputCapture(Console.Error);

            ErrorAnsiConsole = AnsiConsole.Create(new()
            {
                Interactive = InteractionSupport.No,
                Out = new CustomAnsiConsoleOutput(errorCapture),
            });

            AppXManifestManager = new Mock<AppXManifestManager>(args: ErrorAnsiConsole) { CallBase = true };

            return errorCapture;
        }

        private Func<InvocationContext, Task>? _testCallback;

        private async Task TestAsync(InvocationContext invocationContext)
        {
            if (_testCallback != null)
            {
                await _testCallback(invocationContext);
            }
        }

        private sealed class TestCommand : Command
        {
            private readonly BaseCommandLineTest _baseCommandLineTest;

            public TestCommand(BaseCommandLineTest baseCommandLineTest)
                : base("test")
            {
                _baseCommandLineTest = baseCommandLineTest;
                this.SetHandler(_baseCommandLineTest.TestAsync);
            }
        }

        internal sealed class OutputCapture : TextWriter, IStandardStreamWriter, IDisposable
        {
#pragma warning disable CA2213 // Disposable fields should be disposed
            private readonly TextWriter _stdOutWriter;
#pragma warning restore CA2213 // Disposable fields should be disposed
            public TextWriter Captured { get; private set; }
            public override Encoding Encoding => Encoding.ASCII;

            public OutputCapture(TextWriter textWriter)
            {
                _stdOutWriter = textWriter;
                Console.SetOut(this);
                Captured = new StringWriter();
            }

            public override void Write(string? value)
            {
                Captured.Write(value);
                _stdOutWriter.Write(value);
            }

            public override void WriteLine(string? value)
            {
                Captured.WriteLine(value);
                _stdOutWriter.WriteLine(value);
            }
        }

        internal sealed class TestConsole(BaseCommandLineTest.OutputCapture outputCapture, BaseCommandLineTest.OutputCapture errorCapture) : IConsole
        {
            public IStandardStreamWriter Error { get; set; } = errorCapture;
            public IStandardStreamWriter Out { get; set; } = outputCapture;
            public bool IsOutputRedirected { get; set; }
            public bool IsErrorRedirected { get; set; }
            public bool IsInputRedirected { get; set; }
        }

        internal sealed class CustomAnsiConsoleOutput(TextWriter writer) : IAnsiConsoleOutput
        {
            public TextWriter Writer { get; } = writer ?? throw new ArgumentNullException(nameof(writer));
            public bool IsTerminal => false;
            public int Width => 260;
            public int Height => 80;

            public void SetEncoding(Encoding encoding)
            {
            }
        }
    }
}