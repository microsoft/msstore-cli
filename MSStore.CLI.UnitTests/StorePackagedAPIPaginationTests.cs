// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using MSStore.API.Models;
using MSStore.API.Packaged;
using MSStore.API.Packaged.Models;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class StorePackagedAPIPaginationTests
    {
        private static PagedResponse<DevCenterApplication> CreatePage(string[] applicationIds, string? nextLink, int totalCount)
        {
            return new PagedResponse<DevCenterApplication>
            {
                NextLink = nextLink,
                TotalCount = totalCount,
                Value = applicationIds.Select(id => new DevCenterApplication { Id = id }).ToList(),
            };
        }

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void PagedResponse_DeserializesNextLinkAndTotalCount_UsingSourceGenerationContext()
        {
            const string json = "{\"@nextLink\":\"https://example.test/applications?skip=100\",\"totalCount\":101,\"value\":[{\"id\":\"first\"}]}";

            var response = JsonSerializer.Deserialize(json, SourceGenerationContext.GetCustom().PagedResponseDevCenterApplication);

            response.Should().NotBeNull();
            response!.NextLink.Should().Be("https://example.test/applications?skip=100");
            response.TotalCount.Should().Be(101);
            response.Value.Should().ContainSingle().Which.Id.Should().Be("first");
        }

        [TestMethod]
        public async Task GetAllPagesAsync_AggregatesShortPages()
        {
            var requestedPages = new List<(int Skip, int Top)>();
            var responses = new Queue<PagedResponse<DevCenterApplication>>([
                CreatePage(["first", "second"], "https://example.test/applications?skip=2", 3),
                CreatePage(["third"], null, 3),
            ]);

            var applications = await StorePackagedAPI.GetAllPagesAsync<DevCenterApplication>(
                (skip, top, _) =>
                {
                    requestedPages.Add((skip, top));
                    return Task.FromResult(responses.Dequeue());
                },
                TestContext.CancellationToken).ToListAsync(TestContext.CancellationToken);

            applications.Select(application => application.Id).Should().Equal("first", "second", "third");
            requestedPages.Should().Equal((0, 100), (2, 100));
        }

        [TestMethod]
        public async Task GetAllPagesAsync_StopsWhenEmptyPageHasNextLink()
        {
            var requestCount = 0;

            var applications = await StorePackagedAPI.GetAllPagesAsync<DevCenterApplication>(
                (_, _, _) =>
                {
                    requestCount++;
                    return Task.FromResult(CreatePage([], "https://example.test/applications?skip=100", 200));
                },
                TestContext.CancellationToken).ToListAsync(TestContext.CancellationToken);

            applications.Should().BeEmpty();
            requestCount.Should().Be(1);
        }

        [TestMethod]
        public Task GetAllPagesAsync_ContinuesWhenTotalCountIsAbsent() =>
            AssertContinuesWhenTotalCountIsAbsentOrZeroAsync("{\"@nextLink\":\"https://example.test/applications?skip=1\",\"value\":[{\"id\":\"first\"}]}");

        [TestMethod]
        public Task GetAllPagesAsync_ContinuesWhenTotalCountIsZero() =>
            AssertContinuesWhenTotalCountIsAbsentOrZeroAsync("{\"@nextLink\":\"https://example.test/applications?skip=1\",\"totalCount\":0,\"value\":[{\"id\":\"first\"}]}");

        private async Task AssertContinuesWhenTotalCountIsAbsentOrZeroAsync(string firstPageJson)
        {
            var firstPage = JsonSerializer.Deserialize(firstPageJson, SourceGenerationContext.GetCustom().PagedResponseDevCenterApplication)!;
            var requestCount = 0;

            var applications = await StorePackagedAPI.GetAllPagesAsync<DevCenterApplication>(
                (_, _, _) =>
                {
                    requestCount++;
                    return Task.FromResult(requestCount == 1 ? firstPage : CreatePage(["second"], null, 0));
                },
                TestContext.CancellationToken).ToListAsync(TestContext.CancellationToken);

            applications.Select(application => application.Id).Should().Equal("first", "second");
            requestCount.Should().Be(2);
        }
    }
}
