// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Helpers
{
    internal static class StandardOutput
    {
        /// <summary>
        /// Writes machine-readable output (JSON payloads, paths) directly to stdout.
        /// </summary>
        /// <param name="value">The text to write.</param>
        /// <remarks>
        /// This deliberately bypasses Spectre.Console's <see cref="Spectre.Console.AnsiConsole"/>: its renderer
        /// word-wraps at the console width (falling back to 80 columns when stdout is redirected), which injects
        /// raw newline characters inside JSON string values and produces invalid JSON.
        /// </remarks>
        public static void WriteLine(string value)
        {
            Console.Out.WriteLine(value);
            Console.Out.Flush();
        }
    }
}
