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
        Task<string?> ReadNextAsync(bool hidden, CancellationToken ct);
        Task<string> RequestStringAsync(string fieldName, bool hidden, CancellationToken ct);
        Task<bool> YesNoConfirmationAsync(string message, CancellationToken ct);
        Task<T> SelectionPromptAsync<T>(string title, IEnumerable<T> choices, int pageSize = 10, Func<T, string>? displaySelector = null, CancellationToken ct = default)
            where T : notnull;
    }
}
