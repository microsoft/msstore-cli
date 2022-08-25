// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged.Models;
using MSStore.CLI.Commands;
using MSStore.CLI.Commands.Init.Setup;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.PWABuilder;
using MSStore.CLI.Services.TokenManager;
using MSStore.CLI.UnitTests.Fakes;
using Spectre.Console;

namespace MSStore.CLI.UnitTests
{
    public class BaseCommandLineTest
    {
        internal MicrosoftStoreCLI Cli { get; private set; } = null!;
        internal FakeConsoleReader FakeConsole { get; private set; } = null!;
        internal FakeConfigurationManager FakeConfigurationManager { get; private set; } = null!;
        internal FakePartnerCenterManager FakePartnerCenterManager { get; private set; } = null!;
        internal FakeBrowserLauncher FakeBrowserLauncher { get; private set; } = null!;
        internal FakeCredentialManager FakeCredentialManager { get; private set; } = null!;
        internal FakeExternalCommandExecutor FakeExternalCommandExecutor { get; private set; } = null!;
        internal FakeStoreAPIFactory FakeStoreAPIFactory { get; private set; } = null!;

        protected List<DevCenterApplication> FakeApps { get; } = new List<DevCenterApplication>
            {
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
            };

        private TaskCompletionSource _tcs = null!;
        private Parser _parser = null!;

        [TestInitialize]
        public void Initialize()
        {
            FakeBrowserLauncher = new FakeBrowserLauncher();
            FakeConfigurationManager = new FakeConfigurationManager();
            FakeCredentialManager = new FakeCredentialManager();
            FakeStoreAPIFactory = new FakeStoreAPIFactory(FakeConfigurationManager, FakeCredentialManager);
            Cli = new MicrosoftStoreCLI();

            _tcs = new TaskCompletionSource();

            Cli.AddCommand(new TestCommand(this));

            var builder = new CommandLineBuilder(Cli);
            _parser = builder.UseHost(_ => Host.CreateDefaultBuilder(null), (builder) => builder
                .UseEnvironment("CLI")
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    services.AddSingleton<IConfigurationManager>(FakeConfigurationManager)
                        .AddSingleton<IBrowserLauncher>(FakeBrowserLauncher)
                        .AddSingleton<ICredentialManager>(FakeCredentialManager)
                        .AddSingleton<IConsoleReader, FakeConsoleReader>()
                        .AddSingleton<IExternalCommandExecutor, FakeExternalCommandExecutor>()
                        .AddSingleton<IProjectConfiguratorFactory, ProjectConfiguratorFactory>()
                        .AddScoped<IProjectConfigurator, FlutterProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, UWPProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, PWAProjectConfigurator>()
                        .AddScoped<ICLIConfigurator, CLIConfigurator>()
                        .AddSingleton<IStoreAPIFactory>(FakeStoreAPIFactory)
                        .AddScoped<IPWABuilderClient, FakePWABuilderClient>()
                        .AddScoped<IAzureBlobManager, FakeAzureBlobManager>()
                        .AddScoped<IGraphClient, FakeGraphClient>()
                        .AddScoped<IPartnerCenterManager, FakePartnerCenterManager>()
                        .AddScoped<IZipFileManager, FakeZipFileManager>()
                        .AddScoped<ITokenManager, FakeTokenManager>()
                        .AddSingleton(Cli);
                })
                .ConfigureStoreCLICommands()
                .ConfigureLogging((hostContext, logging) =>
                {
                    var configuration = hostContext.Configuration;
                    logging.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    });
                    logging.SetMinimumLevel(LogLevel.Debug);
                }))
                .AddMiddleware(
                    async (context, next) =>
                    {
                        var ct = context.GetCancellationToken();

                        var host = context.GetHost();
                        FakeConsole = (FakeConsoleReader)host.Services.GetService<IConsoleReader>()!;
                        FakeExternalCommandExecutor = (FakeExternalCommandExecutor)host.Services.GetService<IExternalCommandExecutor>()!;
                        FakePartnerCenterManager = (FakePartnerCenterManager)host.Services.GetService<IPartnerCenterManager>()!;

                        _tcs.TrySetResult();

                        var configurationManager = host.Services.GetService<IConfigurationManager>()!;
                        var credentialManager = host.Services.GetService<ICredentialManager>()!;
                        var consoleReader = host.Services.GetService<IConsoleReader>()!;
                        var cliConfigurator = host.Services.GetService<ICLIConfigurator>()!;
                        var logger = host.Services.GetService<ILogger<Program>>()!;

                        if (context.ParseResult.CommandResult.Command is MicrosoftStoreCLI
                            || context.ParseResult.CommandResult.Command is ReconfigureCommand
                            || context.ParseResult.CommandResult.Command is TestCommand
                            || await MicrosoftStoreCLI.InitAsync(configurationManager, credentialManager, consoleReader, cliConfigurator, logger, ct))
                        {
                            await next(context).ConfigureAwait(false);
                        }
                    }, MiddlewareOrder.Default)
                .Build();
        }

        protected void FakeLogin()
        {
            FakeConfigurationManager.FakeLogin();
            FakeCredentialManager.WriteCredential("testUserName", "testSecret");
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

        internal void AddFakeAccount(AccountEnrollment? accountEnrollment)
        {
            var items = new List<AccountEnrollment>();

            if (accountEnrollment != null)
            {
                items.Add(accountEnrollment);
            }

            FakePartnerCenterManager.SetFakeAccountEnrollments(new AccountEnrollments
            {
                TotalCount = 1,
                Items = items
            });
        }

        protected void AddDefaultFakeSubmission()
        {
            FakeStoreAPIFactory.FakeStorePackagedAPI.SetFakeSubmission(
                new DevCenterSubmission
                {
                    Id = "123456789",
                    ApplicationCategory = DevCenterApplicationCategory.NotSet,
                    FileUploadUrl = "https://azureblob.com/fileupload",
                    ApplicationPackages = new List<ApplicationPackage>
                    {
                        new ApplicationPackage
                        {
                            Id = "123456789",
                            Version = "1.0.0",
                        }
                    },
                    StatusDetails = new StatusDetails
                    {
                        Warnings = new List<CodeAndDetail>
                        {
                            new CodeAndDetail
                            {
                                Code = "Code1",
                                Details = "Detail1"
                            }
                        }
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
                });
        }

        protected void AddFakeApps()
        {
            FakeStoreAPIFactory.FakeStorePackagedAPI.SetFakeApps(FakeApps);
        }

        protected Task<string> RunTestAsync(Func<InvocationContext, Task>? testCallback)
        {
            _testCallback = testCallback;

            return ParseAndInvokeAsync(new[] { "test" }, null);
        }

        protected async Task<string> ParseAndInvokeAsync(string[] args, Func<Task>? func = null, int expectedResult = 0)
        {
            var outputCapture = new OutputCapture();

            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.Detect,
                ColorSystem = ColorSystemSupport.Detect,
                Out = new CustomAnsiConsoleOutput(outputCapture),
            });

            var parseResult = _parser.Parse(args);

            if (parseResult.Errors.Any())
            {
                throw new ArgumentException(string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)));
            }

            var testConsole = new TestConsole(outputCapture);

            var invokeTask = parseResult.InvokeAsync(testConsole);

            await Task.WhenAny(invokeTask, _tcs.Task);

            if (func != null)
            {
                await func().ConfigureAwait(false);
            }

            var result = await invokeTask.ConfigureAwait(false);

            result.Should().Be(expectedResult);

            return outputCapture.Captured.ToString() ?? string.Empty;
        }

        private Func<InvocationContext, Task>? _testCallback;

        private async Task TestAsync(InvocationContext invocationContext)
        {
            if (_testCallback != null)
            {
                await _testCallback(invocationContext);
            }
        }

        private class TestCommand : Command
        {
            private BaseCommandLineTest _baseCommandLineTest;

            public TestCommand(BaseCommandLineTest baseCommandLineTest)
                : base("test")
            {
                _baseCommandLineTest = baseCommandLineTest;
                this.SetHandler(async (invocationContext) =>
                {
                    await _baseCommandLineTest.TestAsync(invocationContext);
                });
            }
        }

        internal class OutputCapture : TextWriter, IStandardStreamWriter, IDisposable
        {
            private TextWriter stdOutWriter;
            public TextWriter Captured { get; private set; }
            public override Encoding Encoding => Encoding.ASCII;

            public OutputCapture()
            {
                stdOutWriter = Console.Out;
                Console.SetOut(this);
                Captured = new StringWriter();
            }

            public override void Write(string? value)
            {
                Captured.Write(value);
                stdOutWriter.Write(value);
            }

            public override void WriteLine(string? value)
            {
                Captured.WriteLine(value);
                stdOutWriter.WriteLine(value);
            }
        }

        internal class TestConsole : IConsole
        {
            public TestConsole(OutputCapture outputCapture)
            {
                Out = outputCapture;
                Error = outputCapture;
            }

            public IStandardStreamWriter Error { get; protected set; }
            public IStandardStreamWriter Out { get; protected set; }
            public bool IsOutputRedirected { get; protected set; }
            public bool IsErrorRedirected { get; protected set; }
            public bool IsInputRedirected { get; protected set; }
        }

        internal sealed class CustomAnsiConsoleOutput : IAnsiConsoleOutput
        {
            public TextWriter Writer { get; }
            public bool IsTerminal => false;
            public int Width => 260;
            public int Height => 80;

            public CustomAnsiConsoleOutput(TextWriter writer)
            {
                Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            public void SetEncoding(Encoding encoding)
            {
            }
        }
    }
}