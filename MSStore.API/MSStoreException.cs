// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace MSStore.API
{
    public class MSStoreException : Exception
    {
        public MSStoreException()
        {
        }

        public MSStoreException(string? message)
            : base(message)
        {
        }

        public MSStoreException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
