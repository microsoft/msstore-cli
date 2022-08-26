// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSStore.API.Packaged.Models
{
    public class CustomJsonStringEnumConverter : JsonConverterFactory
    {
        private readonly JsonNamingPolicy namingPolicy;
        private readonly bool allowIntegerValues;
        private readonly JsonStringEnumConverter baseConverter;

        public CustomJsonStringEnumConverter()
            : this(JsonNamingPolicy.CamelCase, true)
        {
        }

        public CustomJsonStringEnumConverter(JsonNamingPolicy? namingPolicy = null, bool allowIntegerValues = true)
        {
            this.namingPolicy = namingPolicy ?? JsonNamingPolicy.CamelCase;
            this.allowIntegerValues = allowIntegerValues;
            baseConverter = new JsonStringEnumConverter(namingPolicy, allowIntegerValues);
        }

        public override bool CanConvert(Type typeToConvert) => baseConverter.CanConvert(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return new JsonStringEnumConverter(new JsonNamingPolicyDecorator(namingPolicy), allowIntegerValues).CreateConverter(typeToConvert, options);
        }
    }
}
