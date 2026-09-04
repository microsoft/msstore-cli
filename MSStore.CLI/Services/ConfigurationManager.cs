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

        // HRESULTs Windows reports when another process holds the file open.
        private const int ErrorSharingViolation = unchecked((int)0x80070020); // ERROR_SHARING_VIOLATION (32)
        private const int ErrorLockViolation = unchecked((int)0x80070021); // ERROR_LOCK_VIOLATION (33)

        // On Unix, FileShare is implemented with flock(), and .NET surfaces the raw errno
        // (EWOULDBLOCK) as the HResult when the lock cannot be taken. The value differs per platform.
        private const int ErrorWouldBlockLinux = 11; // EAGAIN/EWOULDBLOCK on Linux
        private const int ErrorWouldBlockBsd = 35; // EAGAIN/EWOULDBLOCK on macOS and other BSDs

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

        /// <summary>
        /// Checks whether an <see cref="IOException"/> was caused by another process holding the file open,
        /// as opposed to an unrelated I/O failure that should not be retried or silently ignored.
        /// </summary>
        private static bool IsFileInUse(IOException ex)
        {
            // A missing file/directory is never a sharing violation, even though both derive from IOException.
            if (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }

            return ex.HResult is ErrorSharingViolation or ErrorLockViolation or ErrorWouldBlockLinux or ErrorWouldBlockBsd;
        }

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
            catch (IOException ex) when (IsFileInUse(ex))
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
                catch (IOException ex) when (attempt < MaxOpenAttempts && IsFileInUse(ex))
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
