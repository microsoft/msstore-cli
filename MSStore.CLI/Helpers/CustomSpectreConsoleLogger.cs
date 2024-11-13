// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal class CustomSpectreConsoleLogger(string name, ConsoleFormatter formatter, IExternalScopeProvider? scopeProvider) : ILogger
    {
        internal ConsoleFormatter Formatter { get; set; } = formatter;
        internal IExternalScopeProvider? ScopeProvider { get; set; } = scopeProvider;

        [ThreadStatic]
        private static StringWriter? _stringWriter;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _stringWriter ??= new StringWriter();
            LogEntry<TState> logEntry = new LogEntry<TState>(logLevel, name, eventId, state, exception, formatter);
            Formatter.Write(in logEntry, ScopeProvider, _stringWriter);

            var sb = _stringWriter.GetStringBuilder();
            if (sb.Length == 0)
            {
                return;
            }

            string computedAnsiString = sb.ToString();
            sb.Clear();
            if (sb.Capacity > 1024)
            {
                sb.Capacity = 1024;
            }

            AnsiConsole.Console.Markup(computedAnsiString);
        }
    }
}