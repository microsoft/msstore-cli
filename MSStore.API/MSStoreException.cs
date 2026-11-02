// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;

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

        /// <summary>
        /// Gets the HTTP status of the failed response, when the error came from one that had a
        /// body. Errors without a body are raised as <see cref="MSStoreHttpException"/>, which
        /// carries the whole response instead.
        /// </summary>
        public HttpStatusCode? StatusCode { get; init; }
    }
}
