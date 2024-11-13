// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class ResponseWrapper<T> : ResponseWrapper
    {
        public T? ResponseData { get; set; }

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
