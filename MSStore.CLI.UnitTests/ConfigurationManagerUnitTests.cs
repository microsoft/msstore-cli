// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Telemetry;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ConfigurationManagerUnitTests
    {
        /// <summary>
        /// Signals as soon as the configuration manager logs its first open retry, so tests can
        /// react to an actual retry attempt instead of racing against a fixed delay.
        /// </summary>
        private sealed class RetrySignalingLogger : ILogger<ConfigurationManager<TelemetryConfigurations>>
        {
            private readonly TaskCompletionSource _retryObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task RetryObserved => _retryObserved.Task;

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Match the retry message specifically. The configuration manager also logs at
                // Information when it creates the settings directory, which would otherwise signal
                // before a single retry had happened on a machine that has never run the CLI.
                if (logLevel == LogLevel.Information && formatter(state, exception).Contains("Retrying", StringComparison.Ordinal))
                {
                    _retryObserved.TrySetResult();
                }
            }
        }
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
            var logger = new RetrySignalingLogger();
            var configurationManager = new ConfigurationManager<TelemetryConfigurations>(
                TelemetrySourceGenerationContext.Default.TelemetryConfigurations,
                $"test_{Guid.NewGuid()}.json",
                logger);

            try
            {
                await configurationManager.ClearAsync(TestContext.CancellationToken);

                var otherProcessFile = File.Open(configurationManager.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                var saveTask = configurationManager.SaveAsync(new TelemetryConfigurations { TelemetryEnabled = true }, TestContext.CancellationToken);

                // Wait for an actual retry attempt to be logged, instead of a fixed delay, so this
                // cannot flake if the runner is slow to schedule this thread: as soon as the first
                // retry is observed, the file is released well within the remaining retry budget.
                await logger.RetryObserved.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);

                // The save must not have completed yet, otherwise it did not really
                // wait for the other process to release the file.
                saveTask.IsCompleted.Should().BeFalse();

                otherProcessFile.Dispose();

                await saveTask;

                var telemetryConfigurations = await configurationManager.LoadAsync(true, TestContext.CancellationToken);

                telemetryConfigurations.TelemetryEnabled.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(configurationManager.ConfigPath))
                {
                    File.Delete(configurationManager.ConfigPath);
                }
            }
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
        public async Task LoadAsyncThrowsIfFileIsLockedAndClearInvalidConfigIsDisabled()
        {
            await _configurationManager.ClearAsync(TestContext.CancellationToken);

            using var otherProcessFile = File.Open(_configurationManager.ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            // Callers that need to tell a locked file apart from an invalid one rely on this.
            await Assert.ThrowsExactlyAsync<IOException>(
                () => _configurationManager.LoadAsync(false, TestContext.CancellationToken));
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
