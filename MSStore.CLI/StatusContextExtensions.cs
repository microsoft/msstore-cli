// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using MSStore.API;
using Spectre.Console;

namespace MSStore.CLI
{
    internal static class StatusContextExtensions
    {
        public static void ErrorStatus(this StatusContext _, string message)
        {
            AnsiConsole.MarkupLine($":collision: [bold red]{message.EscapeMarkup()}[/]");
        }

        public static void ErrorStatus(this StatusContext _, Exception exception)
        {
            switch (exception)
            {
                case ArgumentException ex:
                    ErrorStatus(_, ex.Message);
                    break;
                case MSStoreWrappedErrorException ex:
                    var message = ex.Message + Environment.NewLine + string.Join(Environment.NewLine, ex.ResponseErrors);
                    ErrorStatus(_, message);
                    break;
                case Exception _:
                    ErrorStatus(_, "Error!");
                    break;
            }
        }

        public static void SuccessStatus(this StatusContext ctx, string? message = null)
        {
            if (message != null)
            {
                AnsiConsole.MarkupLine($":check_mark_button: {message}");
            }
            else
            {
                AnsiConsole.MarkupLine($":check_mark_button: [bold green]{ctx.Status}[/]");
            }
        }
    }
}
