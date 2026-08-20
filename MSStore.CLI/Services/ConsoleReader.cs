// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace MSStore.CLI.Services
{
    internal class ConsoleReader : IConsoleReader
    {
        public bool IsInputRedirected => Console.IsInputRedirected;

        public async Task<string?> ReadAllStandardInputAsync(TimeSpan? firstByteTimeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using var stream = Console.OpenStandardInput();
            using var reader = new StreamReader(stream);

            var buffer = new char[1];

            var firstCharTask = reader.ReadAsync(buffer, 0, 1);

            int read;
            if (firstByteTimeout.HasValue)
            {
                try
                {
                    using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutSource.CancelAfter(firstByteTimeout.Value);

                    read = await firstCharTask.WaitAsync(timeoutSource.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Standard input is redirected, but nothing was ever written to it.
                    return null;
                }
            }
            else
            {
                read = await firstCharTask.WaitAsync(ct);
            }

            if (read == 0)
            {
                return null;
            }

            // The producer may be slow (it could be another CLI call doing network requests), so
            // the remainder is read without any deadline to make sure nothing is truncated.
            var rest = await reader.ReadToEndAsync(ct);

            return string.Concat(buffer[0].ToString(), rest);
        }

        public async Task<string?> ReadNextAsync(bool hidden, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (hidden)
            {
                StringBuilder input = new();
                while (true)
                {
                    var key = Console.ReadKey(true);
                    ct.ThrowIfCancellationRequested();
                    if (key.Key == ConsoleKey.Enter)
                    {
                        break;
                    }

                    if (key.Key == ConsoleKey.Backspace && input.Length > 0)
                    {
                        input.Remove(input.Length - 1, 1);
                    }
                    else if (key.Key != ConsoleKey.Backspace)
                    {
                        input.Append(key.KeyChar);
                    }
                }

                AnsiConsole.MarkupLine(string.Empty);

                return input.ToString();
            }

            var line = Console.ReadLine();

            // Only way to switch to System.CommandLine context so the cancelation token is
            // in fact cancelled, since Console.ReadLine() is waiting on the current thread.
            await Task.Delay(1, ct);

            ct.ThrowIfCancellationRequested();

            return line;
        }

        public async Task<string> RequestStringAsync(string fieldName, bool hidden, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            AnsiConsole.MarkupLine($"{fieldName}: ");

            return await ReadNextAsync(hidden, ct) ?? string.Empty;
        }

        public async Task<T> SelectionPromptAsync<T>(string title, IEnumerable<T> choices, int pageSize = 10, Func<T, string>? displaySelector = null, CancellationToken ct = default)
            where T : notnull
        {
            return await new SelectionPrompt<T>()
                .Title(title)
                .PageSize(pageSize)
                .MoreChoicesText("[grey](Move up and down to show more)[/]")
                .AddChoices(choices)
                .UseConverter(displaySelector)
                .ShowAsync(AnsiConsole.Console, ct);
        }

        public async Task<bool> YesNoConfirmationAsync(string message, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            return await new ConfirmationPrompt(message)
            {
                DefaultValue = false,
            }
            .ShowAsync(AnsiConsole.Console, ct);
        }
    }
}