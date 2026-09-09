// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
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
using MSStore.CLI.Services.Translation;
using Spectre.Console;

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Shared host, mocks and console capture for command-line tests.
    /// </summary>
    public partial class BaseCommandLineTest
    {
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
        internal Mock<ITranslationService> FakeTranslationService { get; private set; } = null!;
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

        protected List<AppReview> FakeReviews { get; } =
            [
                new AppReview
                {
                    Id = "6BE543FF-1C9C-4534-ACED-AF8B4FBE0316",
                    Date = "3/5/2021 12:48:33 PM",
                    Market = "US",
                    Rating = 5,
                    ReviewerName = "FakeReviewer1",
                    ReviewTitle = "Great app",
                    ReviewText = "This app is great",
                    HelpfulCount = 3,
                    NotHelpfulCount = 0
                },
                new AppReview
                {
                    Id = "7CF654AA-2D8D-4645-BDFE-B09C5FCA1427",
                    Date = "3/6/2021 09:12:01 AM",
                    Market = "BR",
                    Rating = 4,
                    ReviewerName = "FakeReviewer2",
                    ReviewTitle = "Um jogo fantástico",
                    ReviewText = "Gostei muito",
                    ResponseDate = "3/7/2021 10:00:00 AM",
                    ResponseText = "Obrigado!"
                },

                // The analytics API omits fields entirely rather than returning them as null,
                // so at least one fixture has to be sparse.
                new AppReview
                {
                    Id = "8DA765BB-3E7E-4756-CEAF-C1AD6FDB2538",
                    Date = "3/7/2021 08:00:00 PM",
                    Rating = 1
                }
            ];

        internal static Organization DefaultOrganization { get; } = new Organization
        {
            Id = new Guid("F3C1CCB6-09C0-4BAB-BABA-C034BFB60EF9")
        };

        private IHostBuilder _hostBuilder = null!;
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

        private readonly List<string> _temporaryPayloadFiles = [];

        /// <summary>
        /// Writes a payload to a temporary file, so that it can be provided to a command either
        /// through the '--payload' option or as a file path argument. The file is deleted once the
        /// test finishes.
        /// </summary>
        /// <returns>The path of the temporary file.</returns>
        protected string CreateTemporaryPayloadFile(string payload, [CallerMemberName] string caller = null!)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{caller}-{Guid.NewGuid():N}.json");

            File.WriteAllText(path, payload);

            _temporaryPayloadFiles.Add(path);

            return path;
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var temporaryPayloadFile in _temporaryPayloadFiles)
            {
                File.Delete(temporaryPayloadFile);
            }

            _temporaryPayloadFiles.Clear();
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
                    // Mirrors the real implementations, which return an empty string when no credential is
                    // stored for the user - notably after ClearCredentials has emptied the lists.
                    return UserNames.Count > 0 && userName.Equals(UserNames.Last(), StringComparison.OrdinalIgnoreCase)
                        ? Secrets.Last()
                        : string.Empty;
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
                .Setup(fac => fac.CreateWithSecretAsync(It.IsAny<Configurations>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeStoreAPI.Object);
            FakeStoreAPIFactory
                .Setup(fac => fac.CreatePackagedAsync(It.IsAny<Configurations>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FakeStorePackagedAPI.Object);

            StorePackagedAPI.DefaultSubmissionPollDelay = TimeSpan.Zero;
            CLIConfigurator.ValidationRetryDelay = TimeSpan.Zero;

            var azureBlobManagerMock = new Mock<IAzureBlobManager>();
            azureBlobManagerMock
                .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<double>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
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

            FakeTranslationService = new Mock<ITranslationService>();
            FakeTranslationService
                .Setup(x => x.ResolveLanguageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string language, CancellationToken ct) => language);

            _hostBuilder = Host.CreateDefaultBuilder(null)
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
                        .AddSingleton(new TelemetryClient(new TelemetryConfiguration
                        {
                            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000"
                        }))
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
                        .AddScoped(sp => FakeTranslationService.Object);

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
                });
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

        protected void AddDefaultFakeSubmission(string listingDescription = "BaseListingDescription")
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
                                    Description = listingDescription
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

        protected void AddFakeReviews()
        {
            FakeStorePackagedAPI
                .Setup(x => x.GetAppReviewsAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateOnly?>(),
                    It.IsAny<DateOnly?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string productId, DateOnly? startDate, DateOnly? endDate, int? top, int? skip, string? filter, string? orderby, CancellationToken ct) =>
                {
                    var reviews = FakeReviews.AsEnumerable();

                    // Mirrors the subset of the analytics API's filter syntax that the CLI emits.
                    if (filter != null)
                    {
                        var idMatch = System.Text.RegularExpressions.Regex.Match(filter, @"id eq '([^']*)'");
                        if (idMatch.Success)
                        {
                            reviews = reviews.Where(r => string.Equals(r.Id, idMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                        }

                        var ratingMatch = System.Text.RegularExpressions.Regex.Match(filter, @"rating eq (\d+)");
                        if (ratingMatch.Success)
                        {
                            var rating = double.Parse(ratingMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                            reviews = reviews.Where(r => r.Rating == rating);
                        }

                        var marketMatch = System.Text.RegularExpressions.Regex.Match(filter, @"market eq '([^']*)'");
                        if (marketMatch.Success)
                        {
                            reviews = reviews.Where(r => string.Equals(r.Market, marketMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    var value = reviews.Skip(skip ?? 0).Take(top ?? int.MaxValue).ToList();

                    return new PagedResponse<AppReview>
                    {
                        Value = value,
                        TotalCount = value.Count
                    };
                });
        }

        /// <summary>
        /// Makes the fake translation service echo each text back prefixed with the target
        /// language, so tests can assert that translated content reached the output.
        /// </summary>
        protected void AddFakeTranslations(string detectedLanguage = "pt")
        {
            FakeTranslationService
                .Setup(x => x.TranslateAsync(It.IsAny<IReadOnlyList<string?>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<string?> texts, string targetLanguage, CancellationToken ct) =>
                    texts
                        .Select(t => string.IsNullOrWhiteSpace(t)
                            ? null
                            : new TranslationResult($"[{targetLanguage}] {t}", detectedLanguage))
                        .ToList());
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

        protected Task<(string Output, string Error)> RunTestAsync(Func<ParseResult, IHost, CancellationToken, Task>? testCallback)
        {
            _testCallback = testCallback;

            return ParseAndInvokeAsync(["test"]);
        }

        protected async Task<(string Output, string Error)> ParseAndInvokeAsync(string[] args, int? expectedResult = 0)
        {
            var outputCapture = new OutputCapture(Console.Out);
            var errorCapture = RefreshAnsiConsole();

            // Only stdout is redirected: the error capture is reached exclusively through
            // ErrorAnsiConsole, mirroring how Program.cs keeps the two streams apart.
            Console.SetOut(outputCapture);

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

            IHost host = _hostBuilder.Start();

            var storeCLI = host.Services.GetRequiredService<MicrosoftStoreCLI>();
            storeCLI.Subcommands.Add(new TestCommand(this, host));
            var parseResult = storeCLI.Parse(args);

            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            if (parseResult.CommandResult.Command is not MicrosoftStoreCLI
                && parseResult.CommandResult.Command is not ReconfigureCommand
                && parseResult.CommandResult.Command is not TestCommand
                && !await MicrosoftStoreCLI.InitAsync(ErrorAnsiConsole, host.Services.GetService<IConfigurationManager<Configurations>>()!, host.Services.GetService<ICredentialManager>()!, host.Services.GetService<IConsoleReader>()!, host.Services.GetService<ICLIConfigurator>()!, host.Services.GetService<ILogger<Program>>()!, lifetime.ApplicationStopping))
            {
                // Initialization failed
                await host.StopAsync();
                return (Output: string.Empty, Error: string.Empty);
            }

            if (parseResult.Action is ParseErrorAction parseError)
            {
                parseError.ShowTypoCorrections = true;
                parseError.ShowHelp = true;
            }

            parseResult.InvocationConfiguration.Output = outputCapture;
            parseResult.InvocationConfiguration.Error = errorCapture;

            var invokeTask = parseResult.InvokeAsync(parseResult.InvocationConfiguration);

            var result = await invokeTask.ConfigureAwait(false);

            await host.StopAsync();

            if (expectedResult.HasValue)
            {
                result.Should().Be(expectedResult.Value);
            }
            else
            {
                outputCapture.Captured.ToString().Should().NotContain("💥");
            }

            return (Output: StripAnsi(outputCapture.Captured.ToString()), Error: StripAnsi(errorCapture.Captured.ToString()));
        }

        /// <summary>
        /// Removes ANSI escape sequences so assertions can match the visible text.
        /// </summary>
        /// <remarks>
        /// Spectre emits colour and style codes only when the underlying stream negotiates
        /// ANSI support, which differs between a developer machine and CI. Without stripping,
        /// an assertion on a string that spans a markup boundary (for example the "no reviews"
        /// in "This application has [bold][u]no[/] reviews[/].") passes locally and fails on CI.
        /// </remarks>
        /// <param name="value">The captured console output.</param>
        /// <returns>The output with escape sequences removed.</returns>
        private static string StripAnsi(string? value)
        {
            return value == null ? string.Empty : AnsiEscapeSequence().Replace(value, string.Empty);
        }

        [System.Text.RegularExpressions.GeneratedRegex("\u001b\\[[0-9;]*[A-Za-z]")]
        private static partial System.Text.RegularExpressions.Regex AnsiEscapeSequence();

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

        private Func<ParseResult, IHost, CancellationToken, Task>? _testCallback;

        private async Task TestAsync(ParseResult parseResult, IHost host, CancellationToken cancellationToken)
        {
            if (_testCallback != null)
            {
                await _testCallback(parseResult, host, cancellationToken);
            }
        }

        private sealed class TestCommand : Command
        {
            private readonly BaseCommandLineTest _baseCommandLineTest;

            public TestCommand(BaseCommandLineTest baseCommandLineTest, IHost host)
                : base("test")
            {
                _baseCommandLineTest = baseCommandLineTest;
                SetAction((parseResult, ct) => _baseCommandLineTest.TestAsync(parseResult, host, ct));
            }
        }

        internal sealed class OutputCapture : TextWriter, IDisposable
        {
#pragma warning disable CA2213 // Disposable fields should be disposed
            private readonly TextWriter _stdOutWriter;
#pragma warning restore CA2213 // Disposable fields should be disposed
            public TextWriter Captured { get; private set; }
            public override Encoding Encoding => Encoding.ASCII;

            public OutputCapture(TextWriter textWriter)
            {
                _stdOutWriter = textWriter;
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