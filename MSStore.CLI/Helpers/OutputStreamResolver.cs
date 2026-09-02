// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using MSStore.CLI.Services;

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// Resolves which standard stream human-readable output should be written to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolution order is <c>--output-stream</c> &gt; <see cref="EnvironmentInfo.OutputStreamEnvironmentVariable"/>
    /// &gt; <see cref="OutputStream.Stderr"/>. The flag deliberately wins so that a pipeline-wide environment
    /// variable can be overridden on the individual commands that emit a machine-readable payload.
    /// </para>
    /// <para>
    /// <see cref="Program"/> has to build the <see cref="Spectre.Console.IAnsiConsole"/> before the command line is
    /// parsed, because the host builder needs it in the service collection. The raw arguments are therefore
    /// inspected here, the same way <c>--verbose</c> is handled.
    /// </para>
    /// </remarks>
    internal static class OutputStreamResolver
    {
        internal const string OptionName = "--output-stream";

        private static readonly char[] InlineValueSeparators = [':', '='];

        /// <summary>
        /// Resolves the stream from the raw command line arguments and the environment.
        /// </summary>
        /// <param name="args">The raw command line arguments.</param>
        /// <returns>The resolved stream, and a warning to surface when the environment variable is malformed.</returns>
        public static (OutputStream Stream, string? Warning) Resolve(IReadOnlyList<string> args)
        {
            string? environmentValue;
            try
            {
                environmentValue = Environment.GetEnvironmentVariable(EnvironmentInfo.OutputStreamEnvironmentVariable);
            }
            catch (Exception)
            {
                // Reading the environment can throw under restricted hosts. Fall back to the default.
                environmentValue = null;
            }

            return Resolve(args, environmentValue);
        }

        /// <summary>
        /// Resolves the stream from the raw command line arguments and an explicit environment variable value.
        /// </summary>
        /// <param name="args">The raw command line arguments.</param>
        /// <param name="environmentValue">The value of the environment variable, or null when it is not set.</param>
        /// <returns>The resolved stream, and a warning to surface when the environment variable is malformed.</returns>
        public static (OutputStream Stream, string? Warning) Resolve(IReadOnlyList<string> args, string? environmentValue)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (TryParse(FindOptionValue(args), out var fromArgs))
            {
                return (fromArgs, null);
            }

            if (string.IsNullOrWhiteSpace(environmentValue))
            {
                return (OutputStream.Stderr, null);
            }

            if (TryParse(environmentValue, out var fromEnvironment))
            {
                return (fromEnvironment, null);
            }

            return (
                OutputStream.Stderr,
                $"'{environmentValue}' is not a valid {EnvironmentInfo.OutputStreamEnvironmentVariable} value. Expected '{nameof(OutputStream.Stdout)}' or '{nameof(OutputStream.Stderr)}'. Falling back to '{nameof(OutputStream.Stderr)}'.");
        }

        /// <summary>
        /// Parses a stream name, accepting any casing.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="outputStream">The parsed stream.</param>
        /// <returns>True when the value names a known stream.</returns>
        public static bool TryParse(string? value, out OutputStream outputStream)
        {
            outputStream = OutputStream.Stderr;

            return !string.IsNullOrWhiteSpace(value)
                && Enum.TryParse(value.Trim(), ignoreCase: true, out outputStream)
                && Enum.IsDefined(outputStream);
        }

        /// <summary>
        /// Finds the value of the last <c>--output-stream</c> occurrence, supporting both the
        /// <c>--output-stream value</c> and <c>--output-stream=value</c> forms.
        /// </summary>
        /// <param name="args">The raw command line arguments.</param>
        /// <returns>The value, or null when the option is absent.</returns>
        private static string? FindOptionValue(IReadOnlyList<string> args)
        {
            string? value = null;

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (arg.Length > OptionName.Length
                    && arg.StartsWith(OptionName, StringComparison.Ordinal)
                    && Array.IndexOf(InlineValueSeparators, arg[OptionName.Length]) >= 0)
                {
                    value = arg[(OptionName.Length + 1)..];
                }
                else if (string.Equals(arg, OptionName, StringComparison.Ordinal) && i + 1 < args.Count)
                {
                    value = args[i + 1];
                    i++;
                }
            }

            return value;
        }
    }
}
