// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API;
using MSStore.CLI.Services;

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// Resolves the JSON payload of the commands that update a submission.
    /// </summary>
    /// <remarks>
    /// Passing the payload inline is limited by the maximum command line length of the operating
    /// system (32,767 characters on Windows), which a real store listing can easily exceed, so the
    /// payload can also be provided through a file or through the standard input stream.
    /// </remarks>
    internal static class PayloadResolver
    {
        /// <summary>
        /// The value that, when used in place of the payload argument, means "read the payload from
        /// the standard input stream".
        /// </summary>
        /// <remarks>
        /// Piping the payload already works without this token, but only under
        /// <see cref="ImplicitStandardInputTimeout"/>, because a payload that was simply omitted
        /// cannot be told apart from a stream that is redirected but will never be written to.
        /// Using this token is the user stating that a payload really is coming, which lets the
        /// CLI wait for it indefinitely. That is what makes
        /// '<c>msstore submission get &lt;productId&gt; | msstore submission update &lt;productId&gt; -</c>'
        /// dependable no matter how long the first command takes.
        /// </remarks>
        internal const string StandardInputToken = "-";

        /// <summary>
        /// How long to wait for the standard input stream when the payload was not provided at all.
        /// The stream can be redirected but never written to, in which case reading it would block
        /// forever, so the command fails with an actionable message instead. Only the first
        /// character is subject to this timeout, so a slow producer is never truncated.
        /// </summary>
        private static readonly TimeSpan ImplicitStandardInputTimeout = TimeSpan.FromSeconds(5);

        public static async Task<string> ResolveAsync(IConsoleReader consoleReader, string? inlineValue, FileInfo? payloadFile, string argumentName, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(consoleReader);

            if (payloadFile != null)
            {
                if (!string.IsNullOrWhiteSpace(inlineValue))
                {
                    throw new MSStoreException($"Both the '{argumentName}' argument and the '--payload' option were provided. Use only one of them.");
                }

                return await ReadFileAsync(payloadFile.FullName, ct);
            }

            if (string.IsNullOrWhiteSpace(inlineValue))
            {
                if (!consoleReader.IsInputRedirected)
                {
                    throw new MSStoreException(MissingPayloadMessage(argumentName));
                }

                var standardInput = await consoleReader.ReadAllStandardInputAsync(ImplicitStandardInputTimeout, ct);

                if (string.IsNullOrWhiteSpace(standardInput))
                {
                    throw new MSStoreException($"{MissingPayloadMessage(argumentName)} Nothing was written to the standard input stream within {ImplicitStandardInputTimeout.TotalSeconds} seconds. If you are piping the output of a command that takes longer than that to produce it, use '{StandardInputToken}' as the '{argumentName}' argument, which waits indefinitely.");
                }

                return standardInput;
            }

            if (inlineValue == StandardInputToken)
            {
                var standardInput = await consoleReader.ReadAllStandardInputAsync(null, ct);

                if (string.IsNullOrWhiteSpace(standardInput))
                {
                    throw new MSStoreException($"No '{argumentName}' was provided through the standard input stream.");
                }

                return standardInput;
            }

            if (LooksLikeJson(inlineValue))
            {
                return inlineValue;
            }

            if (File.Exists(inlineValue))
            {
                return await ReadFileAsync(inlineValue, ct);
            }

            throw new MSStoreException($"The provided '{argumentName}' is neither a JSON payload nor a path to an existing file. {WaysToProvidePayload(argumentName)}");
        }

        private static async Task<string> ReadFileAsync(string path, CancellationToken ct)
        {
            string payload;

            try
            {
                payload = await File.ReadAllTextAsync(path, ct);
            }
            catch (Exception err) when (err is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new MSStoreException($"Could not read the payload file '{path}'.", err);
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new MSStoreException($"The payload file '{path}' is empty.");
            }

            return payload;
        }

        private static bool LooksLikeJson(string value)
        {
            var trimmed = value.AsSpan().TrimStart();

            return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
        }

        private static string MissingPayloadMessage(string argumentName) =>
            $"No '{argumentName}' was provided. {WaysToProvidePayload(argumentName)}";

        private static string WaysToProvidePayload(string argumentName) =>
            $"Provide it as inline JSON, as a path to a JSON file, through the '--payload' option, or through the standard input stream (by piping it, or by using '{StandardInputToken}' as the '{argumentName}' argument).";
    }
}
