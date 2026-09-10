// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ConfigurationManagerUnitTests
    {
        public TestContext TestContext { get; set; } = null!;

        private string _settingsDirectory = null!;
        private string? _originalSettingsDirectory;

        [TestInitialize]
        public void Initialize()
        {
            _originalSettingsDirectory = Environment.GetEnvironmentVariable(ConfigurationManager<Configurations>.SettingsDirectoryEnvironmentVariable);
            _settingsDirectory = Path.Combine(Path.GetTempPath(), $"msstore-cli-tests-{Guid.NewGuid()}");
            Environment.SetEnvironmentVariable(ConfigurationManager<Configurations>.SettingsDirectoryEnvironmentVariable, _settingsDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager<Configurations>.SettingsDirectoryEnvironmentVariable, _originalSettingsDirectory);

            if (Directory.Exists(_settingsDirectory))
            {
                Directory.Delete(_settingsDirectory, true);
            }
        }

        private static ConfigurationManager<Configurations> CreateConfigurationManager()
            => new(ConfigurationsSourceGenerationContext.Default.Configurations, "settings.json", null);

        [TestMethod]
        public void ConfigurationManager_ShouldUseSettingsDirectoryEnvironmentVariable()
        {
            var configurationManager = CreateConfigurationManager();

            configurationManager.ConfigPath.Should().Be(Path.Combine(_settingsDirectory, "settings.json"));
        }

        [TestMethod]
        public void ConfigurationManager_ConfigPathShouldAlwaysBeRooted()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager<Configurations>.SettingsDirectoryEnvironmentVariable, null);

            var configurationManager = CreateConfigurationManager();

            Path.IsPathRooted(configurationManager.ConfigPath).Should().BeTrue();
        }

        [TestMethod]
        public void ConfigurationManager_ShouldIgnoreRelativeSettingsDirectoryEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager<Configurations>.SettingsDirectoryEnvironmentVariable, Path.Combine("relative", "settings"));

            var configurationManager = CreateConfigurationManager();

            Path.IsPathRooted(configurationManager.ConfigPath).Should().BeTrue();
            configurationManager.ConfigPath.Should().NotContain("relative");
        }

        [TestMethod]
        public async Task ConfigurationManager_LoadShouldNotWriteAnythingIfSettingsFileDoesNotExist()
        {
            var configurationManager = CreateConfigurationManager();

            var config = await configurationManager.LoadAsync(false, CancellationToken.None);

            config.SellerId.Should().BeNull();
            Directory.Exists(_settingsDirectory).Should().BeFalse();
            File.Exists(configurationManager.ConfigPath).Should().BeFalse();
        }

        [TestMethod]
        public async Task ConfigurationManager_LoadShouldReturnSavedSettings()
        {
            var configurationManager = CreateConfigurationManager();

            await configurationManager.SaveAsync(
                new Configurations
                {
                    SellerId = 12345,
                    TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
                    ClientId = new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45")
                },
                CancellationToken.None);

            var config = await new ConfigurationManager<Configurations>(ConfigurationsSourceGenerationContext.Default.Configurations, "settings.json", null)
                .LoadAsync(false, CancellationToken.None);

            config.SellerId.Should().Be(12345);
            config.TenantId.Should().Be(new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"));
            config.ClientId.Should().Be(new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45"));
        }

        [TestMethod]
        public async Task ConfigurationManager_LoadShouldThrowIfSettingsAreInvalidAndShouldNotClearThem()
        {
            var configurationManager = CreateConfigurationManager();

            Directory.CreateDirectory(_settingsDirectory);
            await File.WriteAllTextAsync(configurationManager.ConfigPath, "not a json", TestContext.CancellationToken);

            await Assert.ThrowsExactlyAsync<JsonException>(() => configurationManager.LoadAsync(false, CancellationToken.None));

            (await File.ReadAllTextAsync(configurationManager.ConfigPath, TestContext.CancellationToken)).Should().Be("not a json");
        }

        [TestMethod]
        public async Task ConfigurationManager_LoadShouldClearInvalidSettingsIfRequested()
        {
            var configurationManager = CreateConfigurationManager();

            Directory.CreateDirectory(_settingsDirectory);
            await File.WriteAllTextAsync(configurationManager.ConfigPath, "not a json", TestContext.CancellationToken);

            var config = await configurationManager.LoadAsync(true, CancellationToken.None);

            config.SellerId.Should().BeNull();
            (await File.ReadAllTextAsync(configurationManager.ConfigPath, TestContext.CancellationToken)).Should().NotBe("not a json");
        }
    }
}
