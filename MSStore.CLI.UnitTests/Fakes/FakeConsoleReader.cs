// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MSStore.CLI.Services;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeConsoleReader : IConsoleReader
    {
        private readonly BlockingCollection<string> _inputs = new();
        private readonly ILogger _logger;

        public FakeConsoleReader(ILogger<FakeConsoleReader> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> ReadNextAsync(bool hidden, CancellationToken ct)
        {
            var input = await Task.Run(() =>
                _inputs.TryTake(
                        out var result,
                        TimeSpan.FromSeconds(10))
                    ? result
                    : string.Empty).ConfigureAwait(false);

            _logger.LogInformation("Reading next input: '{Input}'", input);

            return input;
        }

        public async Task<string> RequestStringAsync(string fieldName, bool hidden, CancellationToken ct)
        {
            _logger.LogInformation("Asking for field: '{FieldName}'", fieldName);

            return await ReadNextAsync(hidden, ct) ?? string.Empty;
        }

        public async Task<bool> YesNoConfirmationAsync(string message, CancellationToken ct)
        {
            _logger.LogInformation("Asking for confirmation with message: '{Message}'", message);

            var readLine = (await ReadNextAsync(false, ct) ?? string.Empty).ToLowerInvariant();
            return readLine == "yes" || readLine == "y";
        }

        public void AddNextFake(string s)
        {
            _inputs.Add(s);
        }

        public Task<T> SelectionPromptAsync<T>(string title, IEnumerable<T> choices, int pageSize = 10, Func<T, string>? displaySelector = null, CancellationToken ct = default)
            where T : notnull
        {
            return Task.FromResult(choices.First());
        }
    }
}
