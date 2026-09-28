// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Helpers;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class TerminalTextUnitTests
    {
        [TestMethod]
        [DataRow("\u001b]0;pwned\u0007", " ]0;pwned ")]
        [DataRow("a\u0008b", "a b")]
        [DataRow("a\u009bb", "a b")]
        [DataRow("a\u007fb", "a b")]
        [DataRow("a\tb", "a b")]
        public void SanitizeShouldReplaceControlCharactersWithSpaces(string input, string expected)
        {
            TerminalText.Sanitize(input).Should().Be(expected);
        }

        [TestMethod]
        [DataRow("line1\r\nline2", "line1 line2")]
        [DataRow("line1\nline2", "line1 line2")]
        [DataRow("line1\u2028line2", "line1 line2")]
        public void SanitizeShouldFlattenLineBreaks(string input, string expected)
        {
            TerminalText.Sanitize(input).Should().Be(expected);
        }

        [TestMethod]
        [DataRow("Um jogo fantástico")]
        [DataRow("\u0645\u0645\u062a\u0627\u0632")]
        [DataRow("\u59d4\u6258 \ud83d\udc4d")]
        [DataRow("[bold]not markup[/]")]
        public void SanitizeShouldLeaveOrdinaryTextUntouched(string input)
        {
            // Markup is left for EscapeMarkup to handle, and non-Latin scripts and emoji must
            // survive, since reviews come from every market.
            TerminalText.Sanitize(input).Should().Be(input);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void SanitizeShouldTreatMissingTextAsEmpty(string? input)
        {
            TerminalText.Sanitize(input).Should().BeEmpty();
        }
    }
}
