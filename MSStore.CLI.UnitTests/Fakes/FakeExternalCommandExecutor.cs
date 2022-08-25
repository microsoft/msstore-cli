// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeExternalCommandExecutor : IExternalCommandExecutor, IDisposable
    {
        private readonly BlockingCollection<ExternalCommandExecutionResult> _inputs = new();
        private readonly ILogger _logger;

        public FakeExternalCommandExecutor(ILogger<FakeExternalCommandExecutor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExternalCommandExecutionResult> RunAsync(string command, string workingDirectory, CancellationToken ct)
        {
            var input = await Task.Run(() =>
                _inputs.TryTake(
                        out var result,
                        TimeSpan.FromSeconds(10))
                    ? result
                    : new ExternalCommandExecutionResult
                    {
                        ExitCode = -1000
                    });

            _logger.LogInformation("Returning : '{Input}'", input.ExitCode);

            return input;
        }

        public void AddNextFake(ExternalCommandExecutionResult s)
        {
            _inputs.Add(s);
        }

        public void Dispose()
        {
            _inputs.Dispose();
        }
    }
}
