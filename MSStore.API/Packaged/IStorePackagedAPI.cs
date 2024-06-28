// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MSStore.API.Packaged.Models;

namespace MSStore.API.Packaged
{
    public interface IStorePackagedAPI
    {
        Task<DevCenterApplication> GetApplicationAsync(string productId, CancellationToken ct = default);
        Task<List<DevCenterApplication>> GetApplicationsAsync(CancellationToken ct = default);
        Task<DevCenterError?> DeleteSubmissionAsync(string productId, string submissionId, CancellationToken ct = default);
        Task<DevCenterSubmission> CreateSubmissionAsync(string productId, CancellationToken ct = default);
        Task<DevCenterSubmission> GetSubmissionAsync(string productId, string submissionId, CancellationToken ct = default);
        Task<DevCenterSubmission> UpdateSubmissionAsync(string productId, string submissionId, DevCenterSubmission updatedSubmission, CancellationToken ct = default);
        Task<DevCenterCommitResponse?> CommitSubmissionAsync(string productId, string submissionId, CancellationToken ct = default);
        Task<DevCenterSubmissionStatusResponse> GetSubmissionStatusAsync(string productId, string submissionId, CancellationToken ct = default);
        Task<List<DevCenterFlight>> GetFlightsAsync(string productId, CancellationToken ct = default);
        Task<DevCenterFlight> GetFlightAsync(string productId, string flightId, CancellationToken ct = default);
        Task<DevCenterFlightSubmission> GetFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default);
        Task<DevCenterError?> DeleteFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default);
        Task<DevCenterFlightSubmission> CreateFlightSubmissionAsync(string productId, string flightId, CancellationToken ct = default);
        Task<DevCenterFlightSubmission> UpdateFlightSubmissionAsync(string productId, string flightId, string submissionId, DevCenterFlightSubmissionUpdate updatedFlightSubmission, CancellationToken ct = default);
        Task<DevCenterCommitResponse?> CommitFlightSubmissionAsync(string productId, string flightId, string submissionId, CancellationToken ct = default);
        Task<DevCenterSubmissionStatusResponse> GetFlightSubmissionStatusAsync(string productId, string flightId, string submissionId, CancellationToken ct = default);
        Task<PackageRollout> GetPackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default);
        Task<PackageRollout> UpdatePackageRolloutPercentageAsync(string productId, string submissionId, string? flightId, float percentage, CancellationToken ct = default);
        Task<PackageRollout> HaltPackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default);
        Task<PackageRollout> FinalizePackageRolloutAsync(string productId, string submissionId, string? flightId, CancellationToken ct = default);
    }
}