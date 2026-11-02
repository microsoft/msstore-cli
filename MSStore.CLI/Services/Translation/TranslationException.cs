// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.CLI.Services.Translation
{
    /// <summary>
    /// Raised when the translation service cannot fulfil a request. The message is intended
    /// to be shown to the user directly.
    /// </summary>
    internal class TranslationException : Exception
    {
        public TranslationException()
        {
        }

        public TranslationException(string message)
            : base(message)
        {
        }

        public TranslationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
