// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal sealed class CustomSpectreConsoleFormatter : ConsoleFormatter
    {
        private const string LoglevelPadding = ": ";
        private static readonly string _messagePadding = new(' ', GetLogLevelString(LogLevel.Information).Length + LoglevelPadding.Length);
        private static readonly string _newLineWithMessagePadding = Environment.NewLine + _messagePadding;
        internal SimpleConsoleFormatterOptions? FormatterOptions { get; set; }

        public CustomSpectreConsoleFormatter()
            : base(ConsoleFormatterNames.Simple)
        {
        }

        private static void WriteMessage(TextWriter textWriter, string message, bool singleLine)
        {
            if (!string.IsNullOrEmpty(message))
            {
                if (singleLine)
                {
                    textWriter.Write(' ');
                    WriteReplacing(textWriter, Environment.NewLine, " ", message);
                }
                else
                {
                    textWriter.Write(_messagePadding);
                    WriteReplacing(textWriter, Environment.NewLine, _newLineWithMessagePadding, message);
                    textWriter.Write(Environment.NewLine);
                }
            }

            static void WriteReplacing(TextWriter writer, string oldValue, string newValue, string message)
            {
                string newMessage = message.Replace(oldValue, newValue);
                writer.Write(newMessage);
            }
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
            };
        }

        [MemberNotNull(nameof(FormatterOptions))]
        private void ReloadLoggerOptions()
        {
            FormatterOptions = new SimpleConsoleFormatterOptions();
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            LogLevel logLevel = logEntry.LogLevel;
            ConsoleColors logLevelColors = GetLogLevelConsoleColors(logLevel);
            string logLevelString = GetLogLevelString(logLevel);

            string? timestamp = null;
            string? timestampFormat = FormatterOptions?.TimestampFormat;
            if (timestampFormat != null)
            {
                DateTimeOffset dateTimeOffset = GetCurrentDateTime();
                timestamp = dateTimeOffset.ToString(timestampFormat, CultureInfo.InvariantCulture);
            }

            if (timestamp != null)
            {
                textWriter.Write(timestamp);
            }

            static void WriteColoredMessage(TextWriter textWriter, string message, ConsoleColor? background, ConsoleColor? foreground)
            {
                if (background.HasValue || foreground.HasValue)
                {
                    textWriter.Write("[");

                    if (foreground.HasValue)
                    {
                        textWriter.Write(Color.FromConsoleColor(foreground.Value).ToMarkup());
                    }
                    else
                    {
                        textWriter.Write($"default");
                    }

                    if (background.HasValue)
                    {
                        textWriter.Write($" on {Color.FromConsoleColor(background.Value).ToMarkup()}");
                    }

                    textWriter.Write("]");
                }

                textWriter.Write(message);
                if (background.HasValue || foreground.HasValue)
                {
                    textWriter.Write("[/]");
                }
            }

            if (logLevelString != null)
            {
                WriteColoredMessage(textWriter, logLevelString, logLevelColors.Background, logLevelColors.Foreground);
            }

            CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider);
        }

        private void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string message, IExternalScopeProvider? scopeProvider)
        {
            bool singleLine = FormatterOptions?.SingleLine ?? false;
            int eventId = logEntry.EventId.Id;
            Exception? exception = logEntry.Exception;

            // Example:
            // info: ConsoleApp.Program[10]
            //       Request received

            // category and event id
            textWriter.Write(LoglevelPadding);
            textWriter.Write(logEntry.Category.EscapeMarkup());
            textWriter.Write("[[");

            Span<char> span = stackalloc char[10];
            if (eventId.TryFormat(span, out int charsWritten, default, CultureInfo.InvariantCulture))
            {
                textWriter.Write(span[..charsWritten]);
            }
            else
            {
                textWriter.Write(eventId.ToString(CultureInfo.InvariantCulture));
            }

            textWriter.Write("]]");
            if (!singleLine)
            {
                textWriter.Write(Environment.NewLine);
            }

            // scope information
            WriteScopeInformation(textWriter, scopeProvider, singleLine);
            WriteMessage(textWriter, message, singleLine);

            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            if (exception != null)
            {
                // exception message
                WriteMessage(textWriter, exception.ToString().EscapeMarkup(), singleLine);
            }

            if (singleLine)
            {
                textWriter.Write(Environment.NewLine);
            }
        }

        private DateTimeOffset GetCurrentDateTime()
        {
            return FormatterOptions?.UseUtcTimestamp == true ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        }

        private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
        {
            bool disableColors = (FormatterOptions?.ColorBehavior == LoggerColorBehavior.Disabled) ||
                (FormatterOptions?.ColorBehavior == LoggerColorBehavior.Default);
            if (disableColors)
            {
                return new ConsoleColors(null, null);
            }

            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
                LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
                LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
                _ => new ConsoleColors(null, null)
            };
        }

        private void WriteScopeInformation(TextWriter textWriter, IExternalScopeProvider? scopeProvider, bool singleLine)
        {
            if (FormatterOptions?.IncludeScopes == true && scopeProvider != null)
            {
                bool paddingNeeded = !singleLine;
                scopeProvider.ForEachScope(
                    (scope, state) =>
                {
                    if (paddingNeeded)
                    {
                        paddingNeeded = false;
                        state.Write(_messagePadding);
                        state.Write("=> ");
                    }
                    else
                    {
                        state.Write(" => ");
                    }

                    state.Write(scope);
                }, textWriter);

                if (!paddingNeeded && !singleLine)
                {
                    textWriter.Write(Environment.NewLine);
                }
            }
        }

        private readonly struct ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
        {
            public ConsoleColor? Foreground { get; } = foreground;

            public ConsoleColor? Background { get; } = background;
        }
    }
}
