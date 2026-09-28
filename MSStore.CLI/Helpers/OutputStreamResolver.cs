// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
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
    /// <see cref="Program"/> has to build the <see cref="Spectre.Console.IAnsiConsole"/> before the real command
    /// line is parsed, because the host builder needs it in the service collection. The option is parsed here
    /// against a throwaway <see cref="RootCommand"/> instead of scanning the raw arguments, so that the console
    /// selection cannot drift from what the real parser decides.
    /// </para>
    /// </remarks>
    internal static class OutputStreamResolver
    {
        internal const string OptionName = "--output-stream";

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

            var fromArgs = FromArgs(args);
            if (fromArgs.HasValue)
            {
                return (fromArgs.Value, null);
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
        /// <remarks>
        /// Only the two names are accepted. <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> would also
        /// accept the underlying numeric values, which are not part of the documented contract. This backs both the
        /// environment variable and <see cref="MicrosoftStoreCLI.OutputStreamOption"/>'s custom parser, so every
        /// route into the setting agrees.
        /// </remarks>
        public static bool TryParse(string? value, out OutputStream outputStream)
        {
            outputStream = OutputStream.Stderr;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var name = value.Trim();

            if (string.Equals(name, nameof(OutputStream.Stdout), StringComparison.OrdinalIgnoreCase))
            {
                outputStream = OutputStream.Stdout;
                return true;
            }

            return string.Equals(name, nameof(OutputStream.Stderr), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the option from the arguments using System.CommandLine itself.
        /// </summary>
        /// <param name="args">The raw command line arguments.</param>
        /// <returns>The stream the option selects, or null when it is absent or unusable.</returns>
        /// <remarks>
        /// <para>
        /// The real command tree needs the host, which needs the console, which needs this answer. The option is
        /// therefore parsed against a throwaway <see cref="RootCommand"/> carrying only
        /// <see cref="MicrosoftStoreCLI.OutputStreamOption"/>, which is a static with no dependencies. Delegating to
        /// the parser keeps inline separators, casing, repeats and the <c>--</c> end-of-options marker consistent
        /// with the real parse for free.
        /// </para>
        /// <para>
        /// <see cref="Command.TreatUnmatchedTokensAsErrors"/> is disabled so that subcommands, arguments and every
        /// other option land in <see cref="ParseResult.UnmatchedTokens"/> rather than producing errors. Only the
        /// option's own errors are inspected: on this probe those are currently the only errors reachable, but
        /// scoping the check keeps an unrelated mistake elsewhere on the command line from discarding a valid
        /// <c>--output-stream</c> if the probe ever grows.
        /// </para>
        /// </remarks>
        private static OutputStream? FromArgs(IReadOnlyList<string> args)
        {
            var probe = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };

            probe.Options.Add(MicrosoftStoreCLI.OutputStreamOption);

            try
            {
                var result = probe.Parse([.. args]);
                var optionResult = result.GetResult(MicrosoftStoreCLI.OutputStreamOption);

                if (optionResult == null || optionResult.Errors.Any())
                {
                    return null;
                }

                return result.GetValue(MicrosoftStoreCLI.OutputStreamOption);
            }
            catch (Exception)
            {
                // The console has to exist no matter how malformed the command line is. The real parse reports
                // the problem properly a moment later.
                return null;
            }
        }
    }
}
