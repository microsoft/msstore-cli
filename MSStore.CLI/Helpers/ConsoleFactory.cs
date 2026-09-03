// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// Builds the console that every human-readable write goes through.
    /// </summary>
    internal static class ConsoleFactory
    {
        /// <summary>
        /// Creates the console for <paramref name="outputStream"/> and installs it as the static
        /// <see cref="AnsiConsole.Console"/>.
        /// </summary>
        /// <param name="outputStream">The stream human-readable output should be written to.</param>
        /// <returns>The console, which is also registered in the service collection.</returns>
        /// <remarks>
        /// A handful of call sites still reach for the static console — the apps, flights and info tables, the
        /// browser launcher, and every <see cref="Services.ConsoleReader"/> prompt. Installing the same
        /// instance keeps them on the selected stream instead of Spectre's default stdout console, and leaves
        /// stdout to <see cref="StandardOutput"/>.
        /// </remarks>
        public static IAnsiConsole Create(OutputStream outputStream)
        {
            var useStdout = outputStream == OutputStream.Stdout;

            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Interactive = (useStdout ? Console.IsOutputRedirected : Console.IsErrorRedirected) ? InteractionSupport.No : InteractionSupport.Yes,
                Out = new AnsiConsoleOutput(useStdout ? Console.Out : Console.Error)
            });

            AnsiConsole.Console = console;

            return console;
        }
    }
}
