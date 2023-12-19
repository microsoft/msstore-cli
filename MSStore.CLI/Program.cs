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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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

namespace MSStore.CLI
{
    internal class Program
    {
        public static async Task<int> Main(params string[] args)
        {
#if WINDOWS
            Console.OutputEncoding = System.Text.Encoding.UTF8;
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
                        .AddScoped<IProjectConfigurator, WinUIProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, PWAProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, ElectronProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, ReactNativeProjectConfigurator>()
                        .AddScoped<IProjectConfigurator, MauiProjectConfigurator>()
                        .AddScoped<IProjectPublisher, MSIXProjectPublisher>()
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
                        .AddScoped<IElectronManifestManager, ElectronManifestManager>()
                        .AddScoped<INuGetPackageManager, NuGetPackageManager>()
                        .AddScoped<IAppXManifestManager, AppXManifestManager>()
                        .AddSingleton<IEnvironmentInformationService, EnvironmentInformationService>()
                        .AddSingleton(telemetryClient);

                    services
                        .AddHttpClient("Default", client =>
                        {
                            AddMSCorrelationId(client.DefaultRequestHeaders);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });

                    services
                        .AddHttpClient("DefaultLargeUploader", client =>
                        {
                            AddMSCorrelationId(client.DefaultRequestHeaders);
                            client.Timeout = TimeSpan.FromMinutes(10);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });

                    services
                        .AddHttpClient(nameof(GraphClient), client =>
                        {
                            client.BaseAddress = new Uri("https://graph.microsoft.com/");
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });

                    services
                        .AddHttpClient(nameof(PartnerCenterManager), client =>
                        {
                            client.BaseAddress = new Uri("https://api.partnercenter.microsoft.com");
                            AddMSCorrelationId(client.DefaultRequestHeaders);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });

                    void AddPWABuilderDefaultHeaders(HttpRequestHeaders defaultRequestHeaders)
                    {
                        defaultRequestHeaders.Add("Platform-Identifier", "MSStoreCLI");
                        defaultRequestHeaders.Add("Platform-Identifier-Version", typeof(PWABuilderClient).Assembly.GetName().Version?.ToString());
                        defaultRequestHeaders.Add("Correlation-Id", telemetryClient.Context.Session.Id);
                    }

                    void AddMSCorrelationId(HttpRequestHeaders defaultRequestHeaders)
                    {
                        defaultRequestHeaders.Add("ms-correlationid", telemetryClient.Context.Session.Id);
                    }

                    services
                        .AddHttpClient($"{nameof(PWABuilderClient)}/MSIX", client =>
                        {
                            client.BaseAddress = new Uri("https://pwabuilder-windows-docker.azurewebsites.net/msix/");
                            AddPWABuilderDefaultHeaders(client.DefaultRequestHeaders);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });

                    services
                        .AddHttpClient($"{nameof(PWABuilderClient)}/API", client =>
                        {
                            client.BaseAddress = new Uri("https://pwabuilder-apiv2-node.azurewebsites.net/api/");
                            AddPWABuilderDefaultHeaders(client.DefaultRequestHeaders);
                        })
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        {
                            return new HttpClientHandler
                            {
                                CheckCertificateRevocationList = true
                            };
                        });
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddProvider(new CustomSpectreConsoleLoggerProvider());
                    });
                })
                .ConfigureStoreCLICommands()
                .ConfigureLogging((hostContext, logging) =>
                {
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

                        logger.LogInformation("Command is {Command}", context.ParseResult.CommandResult.Command.Name);

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
                .UseHelp()
                .CancelOnProcessTermination()
                .Build();

            if (parser.Parse(args).IsVerbose())
            {
                minimumLogLevel = LogLevel.Information;
            }

            var argList = args.ToList();

            if (Console.IsInputRedirected && !Debugger.IsAttached)
            {
                try
                {
                    using var stream = Console.OpenStandardInput();
                    using StreamReader reader = new StreamReader(stream);
                    var x = await reader.ReadToEndAsync().WaitAsync(new CancellationTokenSource(1000).Token);
                    if (!string.IsNullOrWhiteSpace(x))
                    {
                        argList.Add(x);
                    }
                }
                catch (TaskCanceledException)
                {
                    // If there is no input, we don't want to wait forever
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

            if (telemetryConfigurations.TelemetryEnabled != true)
            {
                telemetryConfiguration.DisableTelemetry = true;
            }

            var telemetryClient = new TelemetryClient(telemetryConfiguration);

            telemetryClient.Context.User.Id = telemetryConfigurations.TelemetryGuid;
            telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
            telemetryClient.Context.Component.Version = typeof(Program).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            telemetryClient.Context.Device.OperatingSystem = RuntimeInformation.RuntimeIdentifier;
            telemetryClient.Context.Cloud.RoleInstance = "-";
            telemetryClient.Context.GetInternalContext().NodeName = "-";

            return telemetryClient;
        }
    }
}