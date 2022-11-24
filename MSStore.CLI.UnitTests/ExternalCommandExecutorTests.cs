// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class ExternalCommandExecutorTests
    {
        private ExternalCommandExecutor _externalCommandExecutor = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _externalCommandExecutor = new ExternalCommandExecutor();
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
