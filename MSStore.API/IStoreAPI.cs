// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Models;

namespace MSStore.API
{
    public interface IStoreAPI
    {
        Task<object> GetDraftAsync(string productId, string? moduleName, string languages, CancellationToken ct = default);
        Task<ListingAssetsResponse> GetDraftListingAssetsAsync(string productId, string languages, CancellationToken ct = default);
        Task<ResponseWrapper<ModuleStatus>> GetModuleStatusAsync(string productId, CancellationToken ct = default);
        Task<PublishingStatus> PollSubmissionStatusAsync(string productId, string pollingSubmissionId, CancellationToken ct = default);
        Task<string> PublishSubmissionAsync(string productId, CancellationToken ct = default);
        Task<UpdateMetadataResponse> UpdateProductPackagesAsync(string productId, UpdatePackagesRequest updatedProductPackages, bool skipInitialPolling = false, CancellationToken ct = default);
        Task<UpdateMetadataResponse> UpdateSubmissionMetadataAsync(string productId, UpdateMetadataRequest submissionMetadata, bool skipInitialPolling = false, CancellationToken ct = default);
    }
}