// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Commands;
using MSStore.CLI.Commands.Init.Setup;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.PWABuilder;
using MSStore.CLI.Services.Telemetry;
using MSStore.CLI.Services.TokenManager;

namespace MSStore.CLI
{
    internal class Program
    {
        public static async Task<int> Main(params string[] args)
        {
#if WINDOWS
            Console.OutputEncoding = System.Text.Encoding.Unicode;
#endif

            var storeCLI = new MicrosoftStoreCLI();

            var minimumLogLevel = LogLevel.Critical;

            var telemetryConfigurationManager = new ConfigurationManager<TelemetryConfigurations>(
                                TelemetrySourceGenerationContext.Default.TelemetryConfigurations,
                                "telemetrySettings.json",
                                null);
            TelemetryConfigurations telemetryConfigurations = await telemetryConfigurationManager.LoadAsync(true, CancellationToken.None);
            TelemetryClient telemetryClient = await CreateTelemetryClientAsync(telemetryConfigurationManager, telemetryConfigurations);

            var builder = new CommandLineBuilder(storeCLI);
            var parser = builder.UseHost(_ => Host.CreateDefaultBuilder(args), (builder) => builder
                .UseEnvironment("CLI")
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    services
                        .AddScoped<IConfigurationManager<Configurations>>(sp =>
                        {
                            return new ConfigurationManager<Configurations>(
                                ConfigurationsSourceGenerationContext.Default.Configurations,
                                "settings.json",
                                sp.GetService<ILogger<ConfigurationManager<Configurations>>>()!);
                        })
                        .AddScoped<IConfigurationManager<TelemetryConfigurations>>(sp => telemetryConfigurationManager)
                        .AddScoped<IBrowserLauncher, BrowserLauncher>()
#if WINDOWS
                        .AddScoped<ICredentialManager, Services.CredentialManager.Windows.CredentialManagerWindows>()
#else
                        .AddScoped<ICredentialManager, Services.CredentialManager.Unix.CredentialManagerUnix>()
#endif
                        .AddScoped<IConsoleReader, ConsoleReader>()
                        .AddScoped<IExternalCommandExecutor, ExternalCommandExecutor>()
                        .AddSingleton<IProjectConfiguratorFactory, ProjectConfiguratorFactory>()
                        .AddScoped<IProjectConfigurator, FlutterProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, UWPProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, PWAProjectConfigurator>()
                        .AddScoped<ICLIConfigurator, CLIConfigurator>()
                        .AddSingleton<IStoreAPIFactory, StoreAPIFactory>()
                        .AddScoped<IPWABuilderClient, PWABuilderClient>()
                        .AddScoped<IGraphClient, GraphClient>()
                        .AddScoped<IAzureBlobManager, AzureBlobManager>()
                        .AddScoped<IZipFileManager, ZipFileManager>()
                        .AddScoped<ITokenManager, MSALTokenManager>()
                        .AddScoped<IPartnerCenterManager, PartnerCenterManager>()
                        .AddScoped<IFileDownloader, FileDownloader>()
                        .AddScoped<IImageConverter, ImageConverter>()
                        .AddScoped<IPWAAppInfoManager, PWAAppInfoManager>()
                        .AddSingleton(telemetryClient);
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
                    logging.SetMinimumLevel(minimumLogLevel);
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
                            || await MicrosoftStoreCLI.InitAsync(configurationManager, credentialManager, consoleReader, cliConfigurator, logger, ct))
                        {
                            await next(context);
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
                .CancelOnProcessTermination()
                .Build();

            if (parser.Parse(args).GetValueForOption(storeCLI.VerboseOption))
            {
                minimumLogLevel = LogLevel.Information;
            }

            var argList = args.ToList();

            if (Console.IsInputRedirected && !Debugger.IsAttached)
            {
                using var stream = Console.OpenStandardInput();

                using StreamReader reader = new StreamReader(stream);
                var x = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(x))
                {
                    argList.Add(x);
                }
            }

            args = argList.ToArray();

            var result = await parser.InvokeAsync(args);

            await telemetryClient.FlushAsync(CancellationToken.None);

            return result;
        }

        private static async Task<TelemetryClient> CreateTelemetryClientAsync(ConfigurationManager<TelemetryConfigurations> telemetryConfigurationManager, TelemetryConfigurations telemetryConfigurations)
        {
            var changed = false;
            if (telemetryConfigurations.TelemetryEnabled.HasValue == false)
            {
                telemetryConfigurations.TelemetryEnabled = true;
                changed = true;
            }

            if (string.IsNullOrEmpty(telemetryConfigurations.TelemetryGuid)
                || telemetryConfigurations.TelemetryGuidDateTime.HasValue == false
                || (DateTime.Now - telemetryConfigurations.TelemetryGuidDateTime) >= TimeSpan.FromHours(24))
            {
                telemetryConfigurations.TelemetryGuid = Guid.NewGuid().ToString();
                telemetryConfigurations.TelemetryGuidDateTime = DateTime.Now;
                changed = true;
            }

            if (changed)
            {
                await telemetryConfigurationManager.SaveAsync(telemetryConfigurations, CancellationToken.None);
            }

            TelemetryConfiguration telemetryConfiguration = TelemetryConfiguration.CreateDefault();

            var telemetryConnectionStringProvider = await TelemetryConnectionStringProvider.LoadAsync(null, default);
            if (telemetryConfigurations.TelemetryEnabled == true && telemetryConnectionStringProvider?.AIConnectionString != null)
            {
                var emptyCS = "#{AI_CONNECTION_STRING}#";
                if (telemetryConnectionStringProvider.AIConnectionString != emptyCS)
                {
                    telemetryConfiguration.ConnectionString = telemetryConnectionStringProvider.AIConnectionString;
                }
            }

            /*
            configuration.EnableAzureInstanceMetadataTelemetryModule = false;
            configuration.EnableDiagnosticsTelemetryModule = false;
            configuration.EnableHeartbeat = false;
            configuration.EnableAdaptiveSampling = false;
            configuration.EnableQuickPulseMetricStream = false;
            configuration.EnableDependencyTrackingTelemetryModule = false;
            configuration.EnablePerformanceCounterCollectionModule = false;
            */

            if (telemetryConfigurations.TelemetryEnabled != true)
            {
                telemetryConfiguration.DisableTelemetry = true;
            }

            var telemetryClient = new TelemetryClient(telemetryConfiguration);

            telemetryClient.Context.User.Id = telemetryConfigurations.TelemetryGuid;
            telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
            telemetryClient.Context.Component.Version = typeof(Program).Assembly.GetName().Version?.ToString();
            telemetryClient.Context.Device.OperatingSystem = RuntimeInformation.RuntimeIdentifier;
            telemetryClient.Context.Cloud.RoleInstance = "-";
            telemetryClient.Context.GetInternalContext().NodeName = "-";

            return telemetryClient;
        }
    }
}