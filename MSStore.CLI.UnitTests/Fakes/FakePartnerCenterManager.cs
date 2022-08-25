// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.CLI.Services.PartnerCenter;

namespace MSStore.CLI.UnitTests.Fakes
{
    internal class FakePartnerCenterManager : IPartnerCenterManager
    {
        private TaskCompletionSource _tcs = new TaskCompletionSource();

        private AccountEnrollments FakeAccountEnrollments { get; set; } = new AccountEnrollments
        {
            Items = new List<AccountEnrollment>
            {
            }
        };

        internal void SetFakeAccountEnrollments(AccountEnrollments fakeAccountEnrollments)
        {
            FakeAccountEnrollments = fakeAccountEnrollments;
            _tcs.TrySetResult();
        }

        public async Task<AccountEnrollments> GetEnrollmentAccountsAsync(CancellationToken ct)
        {
            await _tcs.Task.ConfigureAwait(false);
            return FakeAccountEnrollments;
        }
    }
}
