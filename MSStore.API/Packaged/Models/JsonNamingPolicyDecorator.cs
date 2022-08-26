// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text.Json;

namespace MSStore.API.Packaged.Models
{
    public class JsonNamingPolicyDecorator : JsonNamingPolicy
    {
        private readonly JsonNamingPolicy underlyingNamingPolicy;

        public JsonNamingPolicyDecorator(JsonNamingPolicy underlyingNamingPolicy)
        {
            this.underlyingNamingPolicy = underlyingNamingPolicy;
        }

        public override string ConvertName(string name)
        {
            var value = underlyingNamingPolicy == null ? name : underlyingNamingPolicy.ConvertName(name);

            // Forces 1st letter of enum to be capitalized (required by Store REST API)
            return value.Length >= 1 ? string.Concat(value[0].ToString().ToUpper(CultureInfo.InvariantCulture), value.AsSpan(1)) : value;
        }
    }
}
