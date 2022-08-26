// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MSStore.API.Models
{
    public class ResponseError
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? Target { get; set; }

        public override string ToString()
        {
            return $"{Code} - {Message}";
        }
    }
}