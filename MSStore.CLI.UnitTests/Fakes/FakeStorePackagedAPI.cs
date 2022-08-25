// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakeStorePackagedAPI : IStorePackagedAPI
    {
        public Configurations Config { get; }

        private TaskCompletionSource _appsTcs = new TaskCompletionSource();
        private TaskCompletionSource _submissionsTcs = new TaskCompletionSource();
        private TaskCompletionSource _submissionsStatusTcs = new TaskCompletionSource();

        private List<DevCenterApplication> FakeApps { get; set; } = new();

        private List<DevCenterSubmission> FakeSubmissions { get; set; } = new();

        private Queue<DevCenterSubmissionStatusResponse> FakeSubmissionStatusResponseQueue { get; set; } = new();

        internal void SetFakeApps(List<DevCenterApplication> fakeApps)
        {
            FakeApps = fakeApps;
            _appsTcs.TrySetResult();
        }

        internal void SetFakeSubmission(DevCenterSubmission fakeSubmission)
        {
            FakeSubmissions = new List<DevCenterSubmission>
            {
                fakeSubmission
            };
            _submissionsTcs.TrySetResult();
        }

        internal void InitDefaultSubmissionStatusResponseQueue()
        {
            FakeSubmissionStatusResponseQueue.Enqueue(new DevCenterSubmissionStatusResponse
            {
                Status = "CommitStarted"
            });

            FakeSubmissionStatusResponseQueue.Enqueue(new DevCenterSubmissionStatusResponse
            {
                Status = "CommitStarted"
            });

            FakeSubmissionStatusResponseQueue.Enqueue(new DevCenterSubmissionStatusResponse
            {
                Status = "Published"
            });
            _submissionsStatusTcs.TrySetResult();
        }

        public FakeStorePackagedAPI(Configurations config)
        {
            Config = config;
        }

        public async Task<List<DevCenterApplication>> GetApplicationsAsync(CancellationToken ct)
        {
            await _appsTcs.Task.ConfigureAwait(false);
            return FakeApps;
        }

        public async Task<DevCenterApplication> GetApplicationAsync(string productId, CancellationToken ct)
        {
            await _appsTcs.Task.ConfigureAwait(false);
            return FakeApps.First(a => a.Id == productId);
        }

        public Task<DevCenterError?> DeleteSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<DevCenterSubmission> CreateSubmissionAsync(string productId, CancellationToken ct)
        {
            await _submissionsTcs.Task.ConfigureAwait(false);
            return FakeSubmissions[0];
        }

        public Task<DevCenterCommitResponse> CommitSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            return Task.FromResult(new DevCenterCommitResponse
            {
                Status = "CommitStarted",
            });
        }

        public async Task<DevCenterSubmission> GetSubmissionAsync(string productId, string submissionId, CancellationToken ct)
        {
            await _submissionsTcs.Task.ConfigureAwait(false);
            return FakeSubmissions[0];
        }

        public async Task<DevCenterSubmission> UpdateSubmissionAsync(string productId, string submissionId, DevCenterSubmission updatedSubmission, CancellationToken ct)
        {
            await _submissionsTcs.Task.ConfigureAwait(false);
            return FakeSubmissions[0];
        }

        public async Task<DevCenterSubmissionStatusResponse> GetSubmissionStatusAsync(string productId, string submissionId, CancellationToken ct)
        {
            await _submissionsStatusTcs.Task.ConfigureAwait(false);
            if (FakeSubmissionStatusResponseQueue.TryDequeue(out var result))
            {
                return result;
            }

            throw new NotImplementedException();
        }
    }
}
