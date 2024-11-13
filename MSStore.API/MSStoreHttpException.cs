// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;

namespace MSStore.API
{
    public class MSStoreHttpException(HttpResponseMessage response) : MSStoreException
    {
        public HttpResponseMessage Response { get; private set; } = response;
    }
}
