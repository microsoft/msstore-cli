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
    [TestClass]
    public class OutputStreamProcessTests
    {
        // Emitted by Program's "Command is {Command}" log, which only reaches the console under --verbose and
        // is written through the configured Spectre console, so it lands on whichever stream was selected.
        private const string HumanOutputMarker = "Command is";

        private static readonly List<(string Path, string? Content)> TelemetrySettingsBackups = [];

        /// <summary>
        /// Program.Main loads (and may rewrite) telemetrySettings.json before it even parses --help. The
        /// location comes from Environment.GetFolderPath, which ignores LOCALAPPDATA/HOME on Windows, so the
        /// child cannot simply be pointed at a temporary profile. Snapshot the file instead and put it back,
        /// so running the suite leaves the real configuration exactly as it found it.
        /// </summary>
        /// <param name="context">The test context.</param>
        [ClassInitialize]
        public static void BackUpTelemetrySettings(TestContext context)
        {
            foreach (var path in TelemetrySettingsPaths())
            {
                TelemetrySettingsBackups.Add((path, File.Exists(path) ? File.ReadAllText(path) : null));
            }
        }

        [ClassCleanup]
        public static void RestoreTelemetrySettings()
        {
            foreach (var (path, content) in TelemetrySettingsBackups)
            {
                if (content == null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllText(path, content);
                }
            }

            TelemetrySettingsBackups.Clear();
        }

        private static IEnumerable<string> TelemetrySettingsPaths()
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "MSStore.CLI",
                "telemetrySettings.json");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // ConfigurationManager prefers the native ApplicationSupportDirectory on macOS.
                yield return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "Microsoft",
                    "MSStore.CLI",
                    "telemetrySettings.json");
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
            var cliPath = FindCliExecutable();
            if (cliPath == null)
            {
                Assert.Inconclusive("The MSStore.CLI executable was not found. Build the solution before running this test.");
            }

            var startInfo = new ProcessStartInfo(cliPath)
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

        private static string? FindCliExecutable()
        {
            // The test binary lives in <repo>/MSStore.CLI.UnitTests/bin/<Configuration>/<TargetFramework>,
            // and the CLI is built alongside it under the same configuration and target framework.
            var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            var targetFramework = testOutputDirectory.Name;
            var configuration = testOutputDirectory.Parent?.Name;
            if (configuration == null)
            {
                return null;
            }

            var repositoryRoot = testOutputDirectory;
            while (repositoryRoot != null && !File.Exists(Path.Combine(repositoryRoot.FullName, "MSStore.CLI.sln")))
            {
                repositoryRoot = repositoryRoot.Parent;
            }

            if (repositoryRoot == null)
            {
                return null;
            }

            var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "msstore.exe" : "msstore";
            var cliPath = Path.Combine(repositoryRoot.FullName, "MSStore.CLI", "bin", configuration, targetFramework, fileName);

            return File.Exists(cliPath) ? cliPath : null;
        }
    }
}
