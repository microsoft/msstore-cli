// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if !WINDOWS
using System.Linq;
using System.Runtime.InteropServices;
using MSStore.CLI.Services.CredentialManager.Unix;
#endif

namespace MSStore.CLI.Services
{
    internal class ConfigurationManager<T>(JsonTypeInfo<T> jsonTypeInfo, string fileName, ILogger<ConfigurationManager<T>>? logger) : IConfigurationManager<T>
        where T : new()
    {
        private const int MaxOpenAttempts = 5;

        private static readonly string SettingsDirectory = Path.Combine(GetSystemLocalApplicationDataPath(), "Microsoft", "MSStore.CLI");

        private static string GetSystemLocalApplicationDataPath()
        {
#if !WINDOWS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    // Temporary, until DotNet8 fixes this
                    var dir = NativeMethods.GetDirectories(NativeMethods.NSSearchPathDirectory.ApplicationSupportDirectory, NativeMethods.NSSearchPathDomain.User)?.FirstOrDefault();
                    if (dir != null)
                    {
                        return dir;
                    }
                }
                catch
                {
                }
            }
#endif
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(50);

        private readonly string _settingsPath = Path.Combine(SettingsDirectory, fileName);
        private readonly JsonTypeInfo<T> _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        private readonly ILogger? _logger = logger;

        public string ConfigPath => _settingsPath;

        public async Task<T> LoadAsync(bool clearInvalidConfig, CancellationToken ct)
        {
            try
            {
                EnsureDirectoryExists();
                if (!File.Exists(_settingsPath))
                {
                    return await ClearAsync(ct);
                }

                using var file = await OpenAsync(FileMode.Open, ct);

                return await JsonSerializer.DeserializeAsync(file, _jsonTypeInfo, ct) ?? new T();
            }
            catch (IOException ex)
            {
                // Another process is using the file. Do not overwrite its contents,
                // just fallback to the default configuration.
                _logger?.LogWarning(ex, "Could not read the configuration file: {SettingsPath}", _settingsPath);

                if (!clearInvalidConfig)
                {
                    throw;
                }

                return new T();
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
            using var file = await OpenAsync(FileMode.OpenOrCreate, ct);
            file.SetLength(0);
            await file.FlushAsync(ct);
            file.Position = 0;
            var config = new T();
            await JsonSerializer.SerializeAsync(file, config, _jsonTypeInfo, ct);
            return config;
        }

        public async Task SaveAsync(T config, CancellationToken ct)
        {
            using var file = await OpenAsync(FileMode.OpenOrCreate, ct);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(file, config, _jsonTypeInfo, ct);
        }

        private async Task<FileStream> OpenAsync(FileMode fileMode, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return File.Open(_settingsPath, fileMode, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException ex) when (attempt < MaxOpenAttempts && ex is not FileNotFoundException and not DirectoryNotFoundException)
                {
                    // The file is being used by another process. Wait a bit and try again.
                    _logger?.LogInformation("Configuration file '{SettingsPath}' is in use. Retrying ({Attempt}/{MaxOpenAttempts})...", _settingsPath, attempt, MaxOpenAttempts);

                    await Task.Delay(OpenRetryDelay * attempt, ct);
                }
            }
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
