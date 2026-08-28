// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IConsoleReader
    {
        bool IsInputRedirected { get; }

        Task<string?> ReadNextAsync(bool hidden, CancellationToken ct);

        /// <summary>
        /// Reads the entirety of the standard input stream.
        /// </summary>
        /// <param name="firstByteTimeout">
        /// How long to wait for the first character. Standard input can be redirected but never
        /// written to (CI agents, non-interactive shells, debuggers), in which case reading would
        /// block forever. Pass <c>null</c> to wait indefinitely, which is only appropriate when the
        /// user explicitly asked to read from standard input.
        /// </param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>
        /// The full contents of the standard input stream, or <c>null</c> if the first character
        /// did not arrive within <paramref name="firstByteTimeout"/>. Once the first character
        /// arrives, the remainder is read to the end of the stream without any deadline.
        /// </returns>
        Task<string?> ReadAllStandardInputAsync(TimeSpan? firstByteTimeout, CancellationToken ct);

        Task<string> RequestStringAsync(string fieldName, bool hidden, CancellationToken ct);
        Task<bool> YesNoConfirmationAsync(string message, CancellationToken ct);
        Task<T> SelectionPromptAsync<T>(string title, IEnumerable<T> choices, int pageSize = 10, Func<T, string>? displaySelector = null, CancellationToken ct = default)
            where T : notnull;
    }
}
