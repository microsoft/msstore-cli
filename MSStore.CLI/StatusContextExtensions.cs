// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using MSStore.API;
using Spectre.Console;

namespace MSStore.CLI
{
    internal static class StatusContextExtensions
    {
        public static void ErrorStatus(this StatusContext _, IAnsiConsole ansiConsole, string message)
        {
            ansiConsole.MarkupLine($":collision: [bold red]{message.EscapeMarkup()}[/]");
        }

        public static void ErrorStatus(this StatusContext _, IAnsiConsole ansiConsole, Exception exception)
        {
            switch (exception)
            {
                case ArgumentException ex:
                    ErrorStatus(_, ansiConsole, ex.Message);
                    break;
                case MSStoreWrappedErrorException ex:
                    var message = ex.Message + Environment.NewLine + string.Join(Environment.NewLine, ex.ResponseErrors);
                    ErrorStatus(_, ansiConsole, message);
                    break;
                case Exception:
                    ErrorStatus(_, ansiConsole, "Error!");
                    break;
            }
        }

        public static void SuccessStatus(this StatusContext ctx, IAnsiConsole ansiConsole, string? message = null)
        {
            if (message != null)
            {
                ansiConsole.MarkupLine($":check_mark_button: {message}");
            }
            else
            {
                ansiConsole.MarkupLine($":check_mark_button: [bold green]{ctx.Status}[/]");
            }
        }
    }
}
