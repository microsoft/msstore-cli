// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class ResponseWrapper
    {
        public bool IsSuccess { get; set; }
        public List<ResponseError>? Errors { get; set; }
    }
}
