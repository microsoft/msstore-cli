// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Covers the stream routing configured in <see cref="Program"/>, which the in-process harness cannot
    /// reach: <see cref="BaseCommandLineTest.ParseAndInvokeAsync"/> builds its own consoles and never runs
    /// <c>Main</c>. These run the built executable and read stdout and stderr separately.
    /// </summary>
    /// <remarks>
    /// Opt-in through <c>MSSTORE_RUN_PROCESS_TESTS</c>, which CI sets. Running the real executable also runs
    /// <c>CreateTelemetryClientAsync</c>, which rewrites <c>telemetrySettings.json</c> whenever the telemetry
    /// GUID is missing or older than 24 hours, and <c>ConfigurationManager</c> resolves that path through
    /// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>, which ignores <c>LOCALAPPDATA</c>
    /// and <c>HOME</c> on Windows. There is no way to redirect it at a temporary profile, so rather than
    /// mutate a developer's real configuration these only run where that is harmless.
    /// </remarks>
    [TestClass]
    public class OutputStreamProcessTests
    {
        // Emitted by Program's "Command is {Command}" log, which only reaches the console under --verbose and
        // is written through the configured Spectre console, so it lands on whichever stream was selected.
        private const string HumanOutputMarker = "Command is";

        private const string OptInEnvironmentVariable = "MSSTORE_RUN_PROCESS_TESTS";

        [TestInitialize]
        public void SkipUnlessOptedIn()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OptInEnvironmentVariable)))
            {
                Assert.Inconclusive(
                    $"Set {OptInEnvironmentVariable} to run these tests. They execute the real CLI, which rewrites the telemetry settings of whoever runs them.");
            }
        }

        [TestMethod]
        public async Task DefaultRoutesHumanReadableOutputToStandardError()
        {
            var result = await RunCliAsync(["--verbose", "--help"], null);

            result.StdErr.Should().Contain(HumanOutputMarker);
            result.StdOut.Should().NotContain(HumanOutputMarker);
        }

        [DataRow("--output-stream", "stdout")]
        [DataRow("--output-stream=stdout", null)]
        [DataRow("--output-stream", "STDOUT")]
        [TestMethod]
        public async Task OptionRoutesHumanReadableOutputToStandardOutput(string arg, string? value)
        {
            string[] args = value == null
                ? ["--verbose", arg, "--help"]
                : ["--verbose", arg, value, "--help"];

            var result = await RunCliAsync(args, null);

            result.StdOut.Should().Contain(HumanOutputMarker);
            result.StdErr.Should().NotContain(HumanOutputMarker);
        }

        [TestMethod]
        public async Task EnvironmentVariableRoutesHumanReadableOutputToStandardOutput()
        {
            var result = await RunCliAsync(["--verbose", "--help"], "stdout");

            result.StdOut.Should().Contain(HumanOutputMarker);
            result.StdErr.Should().NotContain(HumanOutputMarker);
        }

        [TestMethod]
        public async Task OptionOverridesTheEnvironmentVariable()
        {
            var result = await RunCliAsync(["--verbose", "--output-stream", "stderr", "--help"], "stdout");

            result.StdErr.Should().Contain(HumanOutputMarker);
            result.StdOut.Should().NotContain(HumanOutputMarker);
        }

        [TestMethod]
        public async Task InvalidEnvironmentVariableFallsBackToStandardErrorWithAWarning()
        {
            var result = await RunCliAsync(["--verbose", "--help"], "1");

            result.StdErr.Should().Contain(HumanOutputMarker);
            result.StdErr.Should().Contain(EnvironmentInfo.OutputStreamEnvironmentVariable);
            result.StdOut.Should().NotContain(HumanOutputMarker);
        }

        [DataRow(null)]
        [DataRow("stdout")]
        [TestMethod]
        public async Task HelpAlwaysGoesToStandardOutput(string? environmentValue)
        {
            // System.CommandLine writes help through InvocationConfiguration.Output, which --output-stream
            // deliberately leaves alone so that `msstore --help | more` keeps working.
            var result = await RunCliAsync(["--help"], environmentValue);

            result.ExitCode.Should().Be(0);
            result.StdOut.Should().Contain("Usage:");
        }

        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string[] args, string? outputStreamEnvironmentValue)
        {
            var startInfo = new ProcessStartInfo(ResolveCliExecutable())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            if (outputStreamEnvironmentValue == null)
            {
                // An ambient value on the machine running the tests must not influence the result.
                startInfo.Environment.Remove(EnvironmentInfo.OutputStreamEnvironmentVariable);
            }
            else
            {
                startInfo.Environment[EnvironmentInfo.OutputStreamEnvironmentVariable] = outputStreamEnvironmentValue;
            }

            using var process = Process.Start(startInfo)!;

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            return (process.ExitCode, await stdOutTask, await stdErrTask);
        }

        /// <summary>
        /// Locates the CLI built alongside this test assembly.
        /// </summary>
        /// <returns>The full path to the executable.</returns>
        /// <remarks>
        /// A miss is a failure rather than a skip: these are the only tests covering the real
        /// <see cref="Program"/> stream wiring, so quietly reporting green would drop that coverage the moment
        /// the build layout changes.
        /// </remarks>
        private static string ResolveCliExecutable()
        {
            // The test binary lives in <repo>/MSStore.CLI.UnitTests/bin/<Configuration>/<TargetFramework>,
            // and the CLI is built alongside it under the same configuration and target framework.
            var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            var targetFramework = testOutputDirectory.Name;
            var configuration = testOutputDirectory.Parent?.Name;

            var repositoryRoot = testOutputDirectory;
            while (repositoryRoot != null && !File.Exists(Path.Combine(repositoryRoot.FullName, "MSStore.CLI.sln")))
            {
                repositoryRoot = repositoryRoot.Parent;
            }

            if (configuration == null || repositoryRoot == null)
            {
                Assert.Fail($"Could not locate MSStore.CLI.sln or the build configuration by walking up from '{AppContext.BaseDirectory}'.");
            }

            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "msstore.exe" : "msstore";
            var cliPath = Path.Combine(repositoryRoot.FullName, "MSStore.CLI", "bin", configuration, targetFramework, fileName);

            if (!File.Exists(cliPath))
            {
                Assert.Fail($"The MSStore.CLI executable was not found at '{cliPath}'. Build MSStore.CLI.sln for configuration '{configuration}' and target framework '{targetFramework}' before running these tests.");
            }

            return cliPath;
        }
    }
}
