// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeStoreAPI : IStoreAPI
    {
        public Configurations Config { get; }

        public FakeStoreAPI(Configurations config)
        {
            Config = config;
        }

        internal UpdateMetadataResponse? UpdateMetadataResponse { get; set; }
        internal object? Draft { get; set; }
        internal ListingAssetsResponse? ListingAssetsResponse { get; set; }
        internal ResponseWrapper<ModuleStatus>? ModuleStatus { get; set; }
        internal PublishingStatus? PublishingStatus { get; set; }

        public Task<object> GetDraftAsync(string productId, string? moduleName, string languages, CancellationToken ct)
        {
            return Draft != null ? Task.FromResult(Draft) : throw new MSStoreException();
        }

        public Task<ListingAssetsResponse> GetDraftListingAssetsAsync(string productId, string languages, CancellationToken ct)
        {
            return ListingAssetsResponse != null ? Task.FromResult(ListingAssetsResponse) : throw new MSStoreException();
        }

        public Task<ResponseWrapper<ModuleStatus>> GetModuleStatusAsync(string productId, CancellationToken ct)
        {
            return ModuleStatus != null ? Task.FromResult(ModuleStatus) : throw new MSStoreException();
        }

        public Task<PublishingStatus> PollSubmissionStatusAsync(string productId, string pollingSubmissionId, CancellationToken ct)
        {
            return PublishingStatus.HasValue ? Task.FromResult(PublishingStatus.Value) : throw new MSStoreException();
        }

        public Task<string> PublishSubmissionAsync(string productId, CancellationToken ct)
        {
            return Task.FromResult("12345");
        }

        public Task<UpdateMetadataResponse> UpdateProductPackagesAsync(string productId, UpdatePackagesRequest updatedProductPackages, bool skipInitialPolling = false, CancellationToken ct = default)
        {
            return UpdateMetadataResponse != null ? Task.FromResult(UpdateMetadataResponse) : throw new MSStoreException();
        }

        public Task<UpdateMetadataResponse> UpdateSubmissionMetadataAsync(string productId, UpdateMetadataRequest submissionMetadata, bool skipInitialPolling = false, CancellationToken ct = default)
        {
            return UpdateMetadataResponse != null ? Task.FromResult(UpdateMetadataResponse) : throw new MSStoreException();
        }

        public Task<List<DevCenterApplication>> GetApplicationsAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterApplication> GetApplicationAsync(string productId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterError?> DeleteSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterSubmission> CreateSubmissionAsync(string productId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterCommitResponse> CommitSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterSubmission> GetSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterSubmission> UpdateSubmissionAsync(string productId, string submissionId, DevCenterSubmission updatedSubmission, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<DevCenterSubmissionStatusResponse> GetSubmissionStatusAsync(string productId, string submissionId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
