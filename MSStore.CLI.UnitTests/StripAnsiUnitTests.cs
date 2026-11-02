// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.CLI.UnitTests
{
    /// <summary>
    /// Guards the escape-sequence stripping that keeps console assertions from depending on
    /// whether the host negotiated ANSI support.
    /// </summary>
    [TestClass]
    public class StripAnsiUnitTests
    {
        private const string Esc = "\u001b";

        [TestMethod]
        public void ShouldRemoveColourAndStyleCodes()
        {
            BaseCommandLineTest.StripAnsi($"This application has {Esc}[1;4mno{Esc}[0m{Esc}[1m reviews{Esc}[0m.")
                .Should().Be("This application has no reviews.");
        }

        [TestMethod]
        public void ShouldRemovePrivateModeCodes()
        {
            // A status spinner hides and shows the cursor with private parameter bytes.
            BaseCommandLineTest.StripAnsi($"{Esc}[?25lRetrieving Reviews{Esc}[?25h")
                .Should().Be("Retrieving Reviews");
        }

        [TestMethod]
        public void ShouldRemoveHyperlinks()
        {
            // Spectre's link markup emits OSC 8 sequences terminated by a string terminator.
            BaseCommandLineTest.StripAnsi($"see {Esc}]8;;https://aka.ms/privacy{Esc}\\the terms{Esc}]8;;{Esc}\\ here")
                .Should().Be("see the terms here");
        }

        [TestMethod]
        public void ShouldRemoveBellTerminatedHyperlinks()
        {
            BaseCommandLineTest.StripAnsi($"{Esc}]8;;https://example.com\u0007link{Esc}]8;;\u0007")
                .Should().Be("link");
        }

        [TestMethod]
        public void ShouldLeavePlainTextUntouched()
        {
            BaseCommandLineTest.StripAnsi("This application has no reviews.")
                .Should().Be("This application has no reviews.");
        }

        [TestMethod]
        public void ShouldTreatNullAsEmpty()
        {
            BaseCommandLineTest.StripAnsi(null).Should().BeEmpty();
        }

        [TestMethod]
        public void ShouldPreserveNonAsciiContent()
        {
            BaseCommandLineTest.StripAnsi($"{Esc}[1mUm jogo fantástico{Esc}[0m")
                .Should().Be("Um jogo fantástico");
        }
    }
}
