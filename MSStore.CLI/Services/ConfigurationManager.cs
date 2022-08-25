// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MSStore.API.Models;

namespace MSStore.CLI.Services
{
    internal class ConfigurationManager : IConfigurationManager
    {
        private static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "MSStore.CLI");
        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private readonly ILogger _logger;

        public ConfigurationManager(ILogger<ConfigurationManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Configurations> LoadAsync(bool clearInvalidConfig, CancellationToken ct)
        {
            try
            {
                EnsureDirectoryExists();
                if (!File.Exists(SettingsPath))
                {
                    return await ClearAsync(ct);
                }

                using var file = File.Open(SettingsPath, FileMode.Open);

                return await JsonSerializer.DeserializeAsync(file, SourceGenerationContext.Default.Configurations, ct) ?? new Configurations();
            }
            catch
            {
                if (!clearInvalidConfig)
                {
                    throw;
                }

                return await ClearAsync(ct);
            }
        }

        public async Task<Configurations> ClearAsync(CancellationToken ct)
        {
            EnsureDirectoryExists();
            using var file = File.Open(SettingsPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            await file.FlushAsync(ct);
            file.Position = 0;
            var config = new Configurations();
            await JsonSerializer.SerializeAsync(file, config, SourceGenerationContext.Default.Configurations, ct);
            return config;
        }

        public async Task SaveAsync(Configurations config, CancellationToken ct)
        {
            using var file = File.Open(SettingsPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(file, config, SourceGenerationContext.Default.Configurations, ct);
        }

        private void EnsureDirectoryExists()
        {
            if (Directory.Exists(SettingsDirectory))
            {
                return;
            }

            _logger.LogInformation("Creating settings directory: {SettingsDirectory}", SettingsDirectory);

            _ = Directory.CreateDirectory(SettingsDirectory);
        }
    }
}
