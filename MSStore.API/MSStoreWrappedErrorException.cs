// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using MSStore.API.Models;

namespace MSStore.API
{
    public class MSStoreWrappedErrorException : MSStoreException
    {
        public List<ResponseError> ResponseErrors { get; }

        public MSStoreWrappedErrorException(string? message, List<ResponseError>? responseErrors)
            : base(message)
        {
            ResponseErrors = responseErrors ?? new List<ResponseError>();
        }

        public MSStoreWrappedErrorException(string? message, List<ResponseError>? responseErrors, Exception? innerException)
            : base(message, innerException)
        {
            ResponseErrors = responseErrors ?? new List<ResponseError>();
        }
    }
}
