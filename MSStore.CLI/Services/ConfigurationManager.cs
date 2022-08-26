// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MSStore.CLI.Services
{
    internal class ConfigurationManager<T> : IConfigurationManager<T>
        where T : new()
    {
        private static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "MSStore.CLI");
        private readonly string _settingsPath;
        private readonly JsonTypeInfo<T> _jsonTypeInfo;
        private readonly ILogger? _logger;

        public ConfigurationManager(JsonTypeInfo<T> jsonTypeInfo, string fileName, ILogger<ConfigurationManager<T>>? logger)
        {
            _settingsPath = Path.Combine(SettingsDirectory, fileName);
            _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
            _logger = logger;
        }

        public async Task<T> LoadAsync(bool clearInvalidConfig, CancellationToken ct)
        {
            try
            {
                EnsureDirectoryExists();
                if (!File.Exists(_settingsPath))
                {
                    return await ClearAsync(ct);
                }

                using var file = File.Open(_settingsPath, FileMode.Open);

                return await JsonSerializer.DeserializeAsync(file, _jsonTypeInfo, ct) ?? new T();
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

        public async Task<T> ClearAsync(CancellationToken ct)
        {
            EnsureDirectoryExists();
            using var file = File.Open(_settingsPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            await file.FlushAsync(ct);
            file.Position = 0;
            var config = new T();
            await JsonSerializer.SerializeAsync(file, config, _jsonTypeInfo, ct);
            return config;
        }

        public async Task SaveAsync(T config, CancellationToken ct)
        {
            using var file = File.Open(_settingsPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(file, config, _jsonTypeInfo, ct);
        }

        private void EnsureDirectoryExists()
        {
            if (Directory.Exists(SettingsDirectory))
            {
                return;
            }

            _logger?.LogInformation("Creating settings directory: {SettingsDirectory}", SettingsDirectory);

            _ = Directory.CreateDirectory(SettingsDirectory);
        }
    }
}
