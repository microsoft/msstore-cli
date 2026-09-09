// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Helpers;
using Spectre.Console;

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Covers the console that <see cref="Program"/> builds, including the static
    /// <see cref="AnsiConsole.Console"/> assignment that the ConsoleReader prompts and the browser launcher
    /// confirmation depend on.
    /// </summary>
    [TestClass]
    public class ConsoleFactoryUnitTests
    {
        private const string Marker = "console-factory-marker";

        private IAnsiConsole _previousConsole = null!;
        private TextWriter _previousOut = null!;
        private TextWriter _previousError = null!;
        private StringWriter _stdOut = null!;
        private StringWriter _stdError = null!;

        [TestInitialize]
        public void Initialize()
        {
            _previousConsole = AnsiConsole.Console;
            _previousOut = Console.Out;
            _previousError = Console.Error;

            // ConsoleFactory captures Console.Out/Console.Error when it builds the AnsiConsoleOutput, so the
            // redirection has to be in place first.
            _stdOut = new StringWriter();
            _stdError = new StringWriter();
            Console.SetOut(_stdOut);
            Console.SetError(_stdError);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Console.SetOut(_previousOut);
            Console.SetError(_previousError);
            AnsiConsole.Console = _previousConsole;
            _stdOut.Dispose();
            _stdError.Dispose();
        }

        [TestMethod]
        public void CreateAndInstallWritesToStandardErrorForStderr()
        {
            var console = ConsoleFactory.CreateAndInstall(OutputStream.Stderr);

            console.WriteLine(Marker);

            _stdError.ToString().Should().Contain(Marker);
            _stdOut.ToString().Should().NotContain(Marker);
        }

        [TestMethod]
        public void CreateAndInstallWritesToStandardOutputForStdout()
        {
            var console = ConsoleFactory.CreateAndInstall(OutputStream.Stdout);

            console.WriteLine(Marker);

            _stdOut.ToString().Should().Contain(Marker);
            _stdError.ToString().Should().NotContain(Marker);
        }

        [TestMethod]
        public void CreateAndInstallInstallsTheConsoleAsTheStaticConsole()
        {
            var console = ConsoleFactory.CreateAndInstall(OutputStream.Stderr);

            AnsiConsole.Console.Should().BeSameAs(console);
        }

        [TestMethod]
        public void StaticWritesFollowStderr()
        {
            // The ConsoleReader prompts and the BrowserLauncher confirmation write through the static
            // console, so it has to honour the selected stream too.
            ConsoleFactory.CreateAndInstall(OutputStream.Stderr);

            AnsiConsole.WriteLine(Marker);

            _stdError.ToString().Should().Contain(Marker);
            _stdOut.ToString().Should().NotContain(Marker);
        }

        [TestMethod]
        public void StaticWritesFollowStdout()
        {
            ConsoleFactory.CreateAndInstall(OutputStream.Stdout);

            AnsiConsole.WriteLine(Marker);

            _stdOut.ToString().Should().Contain(Marker);
            _stdError.ToString().Should().NotContain(Marker);
        }
    }
}
