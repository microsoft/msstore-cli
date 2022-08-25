// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Models
{
    public class ResponseWrapper<T>
    {
        public T? ResponseData { get; set; }
        public bool IsSuccess { get; set; }
        public List<ResponseError>? Errors { get; set; }

        public static implicit operator ResponseWrapper<object>(ResponseWrapper<T> v)
        {
            return new()
            {
                Errors = v.Errors,
                IsSuccess = v.IsSuccess,
                ResponseData = v.ResponseData
            };
        }
    }
}
