// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;

namespace MSStore.CLI.Helpers
{
    /// <summary>
    /// Prepares text that came from outside the CLI for display in a terminal.
    /// </summary>
    internal static class TerminalText
    {
        /// <summary>
        /// Replaces control characters with spaces, so the text is shown rather than acted on.
        /// </summary>
        /// <remarks>
        /// Spectre.Console's EscapeMarkup escapes only its own markup brackets; control characters
        /// pass through to the terminal untouched. Text written by a third party, such as a
        /// customer review, could otherwise carry escape sequences that retitle the window, clear
        /// the screen, embed a hidden hyperlink, or overprint earlier output with backspaces.
        /// Line breaks are flattened as well, since each value is displayed on a single line.
        /// </remarks>
        /// <param name="value">The untrusted text.</param>
        /// <returns>The text with every control character and line break replaced by a space.</returns>
        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // ReplaceLineEndings also covers U+2028 and U+2029, which are not control characters.
            var singleLine = value.ReplaceLineEndings(" ");

            return string.Create(singleLine.Length, singleLine, static (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    span[i] = char.IsControl(source[i]) ? ' ' : source[i];
                }
            });
        }

        /// <summary>
        /// Shortens text to at most <paramref name="maxLength"/> UTF-16 units, followed by an
        /// ellipsis, without cutting a character in half.
        /// </summary>
        /// <remarks>
        /// The cut is made on a text element boundary, so a surrogate pair (most emoji) or a
        /// letter with combining marks is kept whole or dropped whole. Cutting at a fixed index
        /// could leave half a surrogate pair, which is not a valid character.
        /// </remarks>
        /// <param name="value">The text to shorten.</param>
        /// <param name="maxLength">The maximum length before the ellipsis.</param>
        /// <returns>The text unchanged if it fits, otherwise its longest whole-character prefix and "...".</returns>
        public static string Truncate(string value, int maxLength)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Length <= maxLength)
            {
                return value;
            }

            var length = 0;
            while (length < value.Length)
            {
                var next = StringInfo.GetNextTextElementLength(value, length);
                if (length + next > maxLength)
                {
                    break;
                }

                length += next;
            }

            return string.Concat(value.AsSpan(0, length), "...");
        }
    }
}
