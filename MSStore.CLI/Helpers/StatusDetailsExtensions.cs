// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MSStore.API.Packaged.Models;
using Spectre.Console;

namespace MSStore.CLI.Helpers
{
    internal static class StatusDetailsExtensions
    {
        public static void PrintAllTables(this StatusDetails statusDetails, IAnsiConsole ansiConsole, string productId, string submissionId, ILogger? logger)
        {
            if (statusDetails == null)
            {
                return;
            }

            if (statusDetails.Errors != null)
            {
                PrintErrorsTable(ansiConsole, statusDetails.Errors);
            }

            if (statusDetails.Warnings?.Count > 0)
            {
                var onlyLogCodes = new List<string>()
                {
                    "SalesUnsupportedWarning"
                };

                var filteredOut = statusDetails.Warnings.Where(w => !onlyLogCodes.Contains(w.Code!));
                if (filteredOut.Any())
                {
                    var table = new Table
                    {
                        Title = new TableTitle($":warning: [b]Submission Warnings[/]")
                    };
                    table.AddColumns("Code", "Details");
                    foreach (var warning in filteredOut)
                    {
                        table.AddRow($"[bold u]{Markup.Escape(warning?.Code ?? string.Empty)}[/]", Markup.Escape(warning?.Details ?? string.Empty));
                    }

                    ansiConsole.Write(table);
                }

                foreach (var error in statusDetails.Warnings.Where(w => onlyLogCodes.Contains(w.Code!)))
                {
                    logger?.LogInformation("{Code} - {Details}", error.Code, error.Details);
                }
            }

            if (statusDetails.CertificationReports?.Count > 0)
            {
                var table = new Table
                {
                    Title = new TableTitle($":paperclip: [b]Certification Reports[/]")
                };
                table.AddColumns("Date", "Report");
                foreach (var certificationReport in statusDetails.CertificationReports)
                {
                    var url = $"https://partner.microsoft.com/dashboard/products/{productId}/submissions/{submissionId}";
                    table.AddRow($"[bold u]{certificationReport.Date}[/]", $"[link]{url.EscapeMarkup()}[/]");
                }

                ansiConsole.Write(table);
            }
        }

        public static void PrintErrorsTable(IAnsiConsole ansiConsole, IEnumerable<CodeAndDetail> errors)
        {
            if (errors.Any())
            {
                var table = new Table
                {
                    Title = new TableTitle($":red_exclamation_mark: [b]Submission Errors[/]")
                };
                table.AddColumns("Code", "Details");
                foreach (var error in errors)
                {
                    table.AddRow($"[bold u]{Markup.Escape(error?.Code ?? string.Empty)}[/]", Markup.Escape(error?.Details ?? string.Empty));
                }

                ansiConsole.Write(table);
            }
        }
    }
}
