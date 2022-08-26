// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MSStore.API.Packaged.Models;

namespace MSStore.API.Models
{
    /// <summary>
    /// Source Generator Configuration for JSON Serialization/Deserialization.
    /// </summary>
    [JsonSourceGenerationOptions]
    [JsonSerializable(typeof(StoreConfigurations))]
    [JsonSerializable(typeof(ResponseWrapper<object>))]
    [JsonSerializable(typeof(ResponseWrapper<ModuleStatus>))]
    [JsonSerializable(typeof(ResponseWrapper<SubmissionStatus>))]
    [JsonSerializable(typeof(ResponseWrapper<CreateSubmissionResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<ListingAssetsResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<ListingsMetadataResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<PropertiesMetadataResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<AvailabilityMetadataResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<PackagesMetadataResponse>))]
    [JsonSerializable(typeof(ResponseWrapper<UpdateMetadataResponse>))]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(ErrorResponse))]
    [JsonSerializable(typeof(CreateSubmissionResponse))]
    [JsonSerializable(typeof(ListingAssetsResponse))]
    [JsonSerializable(typeof(SubmissionStatus))]
    [JsonSerializable(typeof(ModuleStatus))]
    [JsonSerializable(typeof(ListingsMetadataResponse))]
    [JsonSerializable(typeof(BaseListing))]
    [JsonSerializable(typeof(AvailabilityMetadataResponse))]
    [JsonSerializable(typeof(PropertiesMetadataResponse))]
    [JsonSerializable(typeof(PackagesMetadataResponse))]
    [JsonSerializable(typeof(ResponseError))]
    [JsonSerializable(typeof(List<ResponseError>))]
    [JsonSerializable(typeof(StatusDetails))]
    [JsonSerializable(typeof(UpdateMetadataRequest))]
    [JsonSerializable(typeof(UpdateMetadataResponse))]
    [JsonSerializable(typeof(UpdatePackagesRequest))]
    [JsonSerializable(typeof(PagedResponse<DevCenterApplication>))]
    [JsonSerializable(typeof(DevCenterApplication))]
    [JsonSerializable(typeof(DevCenterSubmission))]
    [JsonSerializable(typeof(DevCenterError))]
    [JsonSerializable(typeof(DevCenterCommitResponse))]
    [JsonSerializable(typeof(DevCenterSubmissionStatusResponse))]
    public partial class SourceGenerationContext : JsonSerializerContext
    {
        private static SourceGenerationContext? _default;
        private static SourceGenerationContext? _defaultPretty;

        public static SourceGenerationContext GetCustom(bool writeIndented = false)
        {
            if (writeIndented)
            {
                return _defaultPretty ??=
                    CreateCustom(writeIndented);
            }
            else
            {
                return _default ??=
                    CreateCustom(writeIndented);
            }

            static SourceGenerationContext CreateCustom(bool writeIndented)
            {
                return new SourceGenerationContext(new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    PropertyNameCaseInsensitive = true,
                    IgnoreReadOnlyFields = false,
                    IgnoreReadOnlyProperties = false,
                    IncludeFields = false,
                    WriteIndented = writeIndented,
                    Converters =
                    {
                        new CustomJsonStringEnumConverter()
                    }
                });
            }
        }
    }
}
