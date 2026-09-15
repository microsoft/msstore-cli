// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MSStore.API.Models;
using MSStore.API.Packaged;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class StorePackagedAPIReviewsUnitTests
    {
        public TestContext TestContext { get; set; } = null!;

        /// <summary>
        /// Paging arguments are validated before the client is used, so an invalid value is
        /// always reported as such rather than reaching the service, which rejects it with an
        /// opaque error.
        /// </summary>
        /// <returns>The API instance under test.</returns>
        private static StorePackagedAPI CreateApi() => new(
            new StoreConfigurations
            {
                SellerId = 1,
                TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
                ClientId = new Guid("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45")
            },
            "fakeSecret",
            null,
            null);

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public async Task GetAppReviewsAsyncShouldRejectNonPositiveTop(int top)
        {
            using var api = CreateApi();

            var act = async () => await api.GetAppReviewsAsync("9PN3ABCDEFGA", top: top, ct: TestContext.CancellationToken);

            (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
                .WithParameterName(nameof(top));
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldRejectTopAboveTheServiceMaximum()
        {
            using var api = CreateApi();

            var act = async () => await api.GetAppReviewsAsync(
                "9PN3ABCDEFGA",
                top: StorePackagedAPI.MaxReviewsPerRequest + 1,
                ct: TestContext.CancellationToken);

            (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
                .WithParameterName("top");
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldRejectNegativeSkip()
        {
            using var api = CreateApi();

            var act = async () => await api.GetAppReviewsAsync("9PN3ABCDEFGA", skip: -1, ct: TestContext.CancellationToken);

            (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
                .WithParameterName("skip");
        }
    }
}
