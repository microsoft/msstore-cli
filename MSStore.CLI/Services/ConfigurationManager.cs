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
        /// <summary>
        /// Environment variable that overrides the directory where the CLI stores its settings files.
        /// Must be set to an absolute path. Useful when the user's local application data folder cannot be
        /// resolved, or is not stable between invocations (containers without a passwd entry, ephemeral
        /// <c>HOME</c> directories, etc).
        /// </summary>
        internal static readonly string SettingsDirectoryEnvironmentVariable = "MSSTORE_SETTINGS_DIRECTORY";

        /// <summary>
        /// Resolves the directory where the settings files live. The returned path is always rooted, so that
        /// it can never be interpreted relative to the current working directory, which would make the settings
        /// files resolve to different locations depending on where the CLI happens to be invoked from.
        /// </summary>
        /// <param name="logger">Logger used to report an unusable override.</param>
        /// <returns>The rooted settings directory path.</returns>
        private static string GetSettingsDirectory(ILogger? logger)
        {
            var settingsDirectoryOverride = Environment.GetEnvironmentVariable(SettingsDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(settingsDirectoryOverride))
            {
                // A relative override would put the settings files at a different place for each working
                // directory the CLI is invoked from, which is exactly what this resolution avoids, so it is
                // ignored rather than honored.
                if (Path.IsPathRooted(settingsDirectoryOverride))
                {
                    return Path.GetFullPath(settingsDirectoryOverride);
                }

                logger?.LogWarning(
                    "Ignoring the {EnvironmentVariable} environment variable: '{SettingsDirectory}' is not an absolute path.",
                    SettingsDirectoryEnvironmentVariable,
                    settingsDirectoryOverride);
            }

            var localApplicationDataPath = GetSystemLocalApplicationDataPath();

            if (string.IsNullOrEmpty(localApplicationDataPath) || !Path.IsPathRooted(localApplicationDataPath))
            {
                // The system could not tell us where the local application data folder is (for instance, on Unix,
                // when neither XDG_DATA_HOME, nor HOME, nor the passwd entry are available). Falling back to a
                // relative path would make the settings file depend on the current working directory, so a
                // rooted, invocation-independent location is used instead.
                localApplicationDataPath = Path.Combine(Path.GetTempPath(), GetTemporarySettingsFolderName());
            }

            return Path.GetFullPath(Path.Combine(localApplicationDataPath, "Microsoft", "MSStore.CLI"));
        }

        /// <summary>
        /// Builds the name of the folder used, inside the temporary folder, when the local application data
        /// folder cannot be resolved. The user name is appended, when it is usable as a folder name, so that
        /// different users on the same machine do not share the same settings folder.
        /// </summary>
        /// <returns>The temporary settings folder name.</returns>
        private static string GetTemporarySettingsFolderName()
        {
            const string FolderName = ".msstore-cli";

            var userName = Environment.UserName;

            if (string.IsNullOrWhiteSpace(userName) || userName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return FolderName;
            }

            return $"{FolderName}-{userName}";
        }

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

        private readonly string _settingsPath = Path.Combine(GetSettingsDirectory(logger), fileName);
        private readonly JsonTypeInfo<T> _jsonTypeInfo = jsonTypeInfo ?? throw new ArgumentNullException(nameof(jsonTypeInfo));
        private readonly ILogger? _logger = logger;

        public string ConfigPath => _settingsPath;

        public async Task<T> LoadAsync(bool clearInvalidConfig, CancellationToken ct)
        {
            try
            {
                // No directory is created here on purpose: loading the configuration must not require write
                // access, so that a missing settings file can always fall back to the default settings.
                if (!File.Exists(_settingsPath))
                {
                    _logger?.LogInformation("Settings file not found at '{SettingsPath}'. Using the default settings.", _settingsPath);

                    return new T();
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
            EnsureDirectoryExists();
            using var file = File.Open(_settingsPath, FileMode.OpenOrCreate);
            file.SetLength(0);
            file.Position = 0;
            await JsonSerializer.SerializeAsync(file, config, _jsonTypeInfo, ct);
        }

        private void EnsureDirectoryExists()
        {
            var settingsDirectory = Path.GetDirectoryName(_settingsPath)!;

            if (Directory.Exists(settingsDirectory))
            {
                return;
            }

            _logger?.LogInformation("Creating settings directory: {SettingsDirectory}", settingsDirectory);

            _ = Directory.CreateDirectory(settingsDirectory);
        }
    }
}
