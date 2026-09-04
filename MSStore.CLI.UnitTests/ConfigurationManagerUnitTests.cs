// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services;
using MSStore.CLI.Services.Telemetry;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ConfigurationManagerUnitTests
    {
        private ConfigurationManager<TelemetryConfigurations> _configurationManager = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Initialize()
        {
            _configurationManager = new ConfigurationManager<TelemetryConfigurations>(
                TelemetrySourceGenerationContext.Default.TelemetryConfigurations,
                $"test_{Guid.NewGuid()}.json",
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_configurationManager.ConfigPath))
            {
                File.Delete(_configurationManager.ConfigPath);
            }
        }

        [TestMethod]
        public async Task SaveAsyncWaitsForOtherProcessToReleaseTheFile()
        {
            await _configurationManager.ClearAsync(TestContext.CancellationToken);

            var otherProcessFile = File.Open(_configurationManager.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var saveTask = _configurationManager.SaveAsync(new TelemetryConfigurations { TelemetryEnabled = true }, TestContext.CancellationToken);

            await Task.Delay(100, TestContext.CancellationToken);

            // The save must not have completed yet, otherwise it did not really
            // wait for the other process to release the file.
            saveTask.IsCompleted.Should().BeFalse();

            otherProcessFile.Dispose();

            await saveTask;

            var telemetryConfigurations = await _configurationManager.LoadAsync(true, TestContext.CancellationToken);

            telemetryConfigurations.TelemetryEnabled.Should().BeTrue();
        }

        [TestMethod]
        public async Task LoadAsyncDoesNotThrowIfFileIsLockedByAnotherProcess()
        {
            await _configurationManager.ClearAsync(TestContext.CancellationToken);
            await _configurationManager.SaveAsync(new TelemetryConfigurations { TelemetryEnabled = true }, TestContext.CancellationToken);

            using var otherProcessFile = File.Open(_configurationManager.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var telemetryConfigurations = await _configurationManager.LoadAsync(true, TestContext.CancellationToken);

            telemetryConfigurations.Should().NotBeNull();

            // The file of the other process should not have been cleared.
            otherProcessFile.Length.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task LoadAsyncRecreatesTheFileIfItContainsInvalidJson()
        {
            await _configurationManager.ClearAsync(TestContext.CancellationToken);

            await File.WriteAllTextAsync(_configurationManager.ConfigPath, "not json", TestContext.CancellationToken);

            var telemetryConfigurations = await _configurationManager.LoadAsync(true, TestContext.CancellationToken);

            telemetryConfigurations.Should().NotBeNull();

            // The invalid file should have been repaired.
            var content = await File.ReadAllTextAsync(_configurationManager.ConfigPath, TestContext.CancellationToken);
            content.Should().NotBe("not json");
        }

        [TestMethod]
        public async Task ConcurrentLoadsAndSavesDoNotThrow()
        {
            var tasks = new List<Task>();

            for (var i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(
                    async () =>
                    {
                        var telemetryConfigurations = await _configurationManager.LoadAsync(true, TestContext.CancellationToken);
                        telemetryConfigurations.TelemetryGuid = Guid.NewGuid().ToString();
                        await _configurationManager.SaveAsync(telemetryConfigurations, TestContext.CancellationToken);
                    },
                    TestContext.CancellationToken));
            }

            // Any exception thrown by a concurrent load/save fails the test.
            await Task.WhenAll(tasks);
        }
    }
}
