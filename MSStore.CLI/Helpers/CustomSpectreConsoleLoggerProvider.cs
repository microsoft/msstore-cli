// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal class CustomSpectreConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentDictionary<string, CustomSpectreConsoleLogger> _loggers = new();
        private ConsoleFormatter _formatter;
        private IExternalScopeProvider? _scopeProvider;

        public CustomSpectreConsoleLoggerProvider(IAnsiConsole stdOut, IAnsiConsole stdErr)
        {
            StdOut = stdOut;
            StdErr = stdErr;
            _formatter = new CustomSpectreConsoleFormatter
            {
                FormatterOptions = new SimpleConsoleFormatterOptions
                {
                    ColorBehavior = LoggerColorBehavior.Enabled,
                    IncludeScopes = true,
                    TimestampFormat = "hh:mm:ss ",
                    SingleLine = true
                }
            };
        }

        public IAnsiConsole StdOut { get; }
        public IAnsiConsole StdErr { get; }

        public ILogger CreateLogger(string name)
        {
            return _loggers.GetOrAdd(name, (name) => new CustomSpectreConsoleLogger(name, _formatter, _scopeProvider, StdOut, StdErr));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;

            foreach (var logger in _loggers)
            {
                logger.Value.ScopeProvider = _scopeProvider;
            }
        }
    }
}
