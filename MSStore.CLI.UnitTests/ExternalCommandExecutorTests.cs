// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using MSStore.CLI.Services;
using Spectre.Console;
using static MSStore.CLI.UnitTests.BaseCommandLineTest;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ExternalCommandExecutorTests
    {
        private ExternalCommandExecutor _externalCommandExecutor = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            var errorCapture = new OutputCapture(Console.Error);

            var ansiConsole = AnsiConsole.Create(new()
            {
                Interactive = InteractionSupport.No,
                Out = new CustomAnsiConsoleOutput(errorCapture),
            });
            _externalCommandExecutor = new ExternalCommandExecutor(ansiConsole, NullLogger<ExternalCommandExecutor>.Instance);
        }

        [TestMethod]
        public async Task ExternalCommandExecutorRunsCommand()
        {
            string command = string.Empty;
            string args = string.Empty;
            ExternalCommandExecutionResult result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = "ver";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                command = "cat";
                args = "/etc/os-release";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                command = "sw_vers";
            }

            result = await _externalCommandExecutor.RunAsync(command, args, ".", CancellationToken.None);

            result.Should().NotBeNull();

            result.ExitCode.Should().Be(0);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result.StdOut.Should().Contain("Microsoft Windows");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                result.StdOut.Should().Contain("NAME=");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                result.StdOut.Should().ContainAny("Mac OS X", "macOS", "macOS Server");
            }

            result.StdErr.Should().BeEmpty();
        }
    }
}
