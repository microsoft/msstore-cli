// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace MSStore.CLI.Services
{
    internal interface IExternalCommandExecutor
    {
        Task<ExternalCommandExecutionResult> RunAsync(string command, string arguments, string workingDirectory, CancellationToken ct);
        Task<string> FindToolAsync(string command, CancellationToken ct);
    }
}
