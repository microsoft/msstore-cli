// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine.Invocation;
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
using MSStore.CLI.Services.Http;
using MSStore.CLI.Services.PartnerCenter;
using MSStore.CLI.Services.PWABuilder;
using MSStore.CLI.Services.Telemetry;
using MSStore.CLI.Services.TokenManager;
using Spectre.Console;

namespace MSStore.CLI
{
    internal class Program
    {
        public static async Task<int> Main(params string[] args)
        {
#if WINDOWS
            Console.OutputEncoding = System.Text.Encoding.UTF8;
#endif

            var minimumLogLevel = LogLevel.Critical;

            var telemetryConfigurationManager = new ConfigurationManager<TelemetryConfigurations>(
                                TelemetrySourceGenerationContext.Default.TelemetryConfigurations,
                                "telemetrySettings.json",
                                null);
            TelemetryConfigurations telemetryConfigurations = await telemetryConfigurationManager.LoadAsync(true, CancellationToken.None);
            TelemetryClient telemetryClient = await CreateTelemetryClientAsync(telemetryConfigurationManager, telemetryConfigurations);
            var ansiConsole = AnsiConsole.Create(new()
            {
                Interactive = Console.IsErrorRedirected ? InteractionSupport.No : InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(Console.Error)
            });

            if (args.Contains(MicrosoftStoreCLI.VerboseOption.Name) || args.Any(MicrosoftStoreCLI.VerboseOption.Aliases.Contains))
            {
                minimumLogLevel = LogLevel.Information;
            }

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime()
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
                        .AddSingleton<IAnsiConsole>(ansiConsole)
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
                            return new RetryAfterHttpHandler
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
                        builder.AddProvider(new CustomSpectreConsoleLoggerProvider(ansiConsole));
                    });
                })
                .ConfigureStoreCLICommands()
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.SetMinimumLevel(minimumLogLevel);
                });

            IHost host = hostBuilder.Start();

            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

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

            args = [.. argList];

            var storeCLI = host.Services.GetRequiredService<MicrosoftStoreCLI>();
            var parseResult = storeCLI.Parse(args);

            var logger = host.Services.GetService<ILogger<Program>>()!;
            logger.LogInformation("Command is {Command}", parseResult.CommandResult.Command.Name);

            if (parseResult.CommandResult.Command is not MicrosoftStoreCLI
                && parseResult.CommandResult.Command is not ReconfigureCommand
                && !await MicrosoftStoreCLI.InitAsync(ansiConsole, host.Services.GetService<IConfigurationManager<Configurations>>()!, host.Services.GetService<ICredentialManager>()!, host.Services.GetService<IConsoleReader>()!, host.Services.GetService<ICLIConfigurator>()!, logger, lifetime.ApplicationStopping))
            {
                // Initialization failed
                await host.StopAsync();
                return -1;
            }

            if (parseResult.Action is ParseErrorAction parseError)
            {
                parseError.ShowTypoCorrections = true;
                parseError.ShowHelp = true;
            }

            var result = await parseResult.InvokeAsync(parseResult.InvocationConfiguration, lifetime.ApplicationStopping);

            await host.StopAsync();

            await telemetryClient.FlushAsync(CancellationToken.None);

            return result;
        }

        private static async Task<TelemetryClient> CreateTelemetryClientAsync(ConfigurationManager<TelemetryConfigurations> telemetryConfigurationManager, TelemetryConfigurations telemetryConfigurations)
        {
            var changed = false;
            if (!telemetryConfigurations.TelemetryEnabled.HasValue)
            {
                telemetryConfigurations.TelemetryEnabled = true;
                changed = true;
            }

            if (string.IsNullOrEmpty(telemetryConfigurations.TelemetryGuid)
                || !telemetryConfigurations.TelemetryGuidDateTime.HasValue
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