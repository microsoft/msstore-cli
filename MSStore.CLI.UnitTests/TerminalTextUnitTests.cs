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

        [TestMethod]
        public void TruncateShouldLeaveTextThatFitsUntouched()
        {
            TerminalText.Truncate("short", 10).Should().Be("short");
            TerminalText.Truncate("exactly10!", 10).Should().Be("exactly10!");
        }

        [TestMethod]
        public void TruncateShouldAddAnEllipsisToLongText()
        {
            TerminalText.Truncate("abcdefghij", 4).Should().Be("abcd...");
        }

        [TestMethod]
        public void TruncateShouldNotSplitASurrogatePair()
        {
            // The emoji is two UTF-16 units and would straddle the limit; cutting at the index
            // would leave half of it, which is not a valid character.
            var text = new string('a', 9) + "\ud83d\udc4d" + "tail";

            TerminalText.Truncate(text, 10).Should().Be(new string('a', 9) + "...");
        }

        [TestMethod]
        public void TruncateShouldKeepACharacterThatFitsExactly()
        {
            var text = new string('a', 8) + "\ud83d\udc4d" + "tail";

            TerminalText.Truncate(text, 10).Should().Be(new string('a', 8) + "\ud83d\udc4d...");
        }

        [TestMethod]
        public void TruncateShouldNotSeparateACombiningMark()
        {
            // "e" followed by a combining acute accent is one character on screen.
            var text = "aaaa" + "e\u0301" + "tail";

            TerminalText.Truncate(text, 5).Should().Be("aaaa...");
        }
    }
}
