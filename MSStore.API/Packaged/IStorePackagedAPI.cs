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
    }
}