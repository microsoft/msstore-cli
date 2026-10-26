// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Microsoft.Identity.Client;
using MSStore.API;
using MSStore.API.Models;
using MSStore.API.Packaged;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class StorePackagedAPIReviewsUnitTests
    {
        private const string EmptyPage = """{"Value":[],"@nextLink":null,"TotalCount":0}""";

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldSendEveryQueryParameter()
        {
            var handler = new CapturingHandler(EmptyPage);
            using var api = CreateInitializedApi(handler);

            await api.GetAppReviewsAsync(
                "9NBLGGH0MTF4",
                new DateOnly(2024, 1, 31),
                new DateOnly(2024, 2, 29),
                top: 50,
                skip: 100,
                filter: "rating eq 1 and market eq 'US'",
                orderby: "date desc",
                ct: TestContext.CancellationToken);

            var uri = handler.RequestUris.Single();
            uri.AbsolutePath.Should().Be("/v1.0/my/analytics/reviews");
            uri.Query.Should().NotContain(" ");

            var query = HttpUtility.ParseQueryString(uri.Query);
            query["applicationId"].Should().Be("9NBLGGH0MTF4");
            query["startDate"].Should().Be("2024-01-31");
            query["endDate"].Should().Be("2024-02-29");
            query["top"].Should().Be("50");
            query["skip"].Should().Be("100");
            query["filter"].Should().Be("rating eq 1 and market eq 'US'");
            query["orderby"].Should().Be("date desc");
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldOmitParametersThatWereNotGiven()
        {
            // Omitting the dates is what makes the service return reviews from every date, so
            // they must not be sent with some default value.
            var handler = new CapturingHandler(EmptyPage);
            using var api = CreateInitializedApi(handler);

            await api.GetAppReviewsAsync("9NBLGGH0MTF4", ct: TestContext.CancellationToken);

            HttpUtility.ParseQueryString(handler.RequestUris.Single().Query).AllKeys
                .Should().Equal("applicationId");
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldEscapeReservedCharacters()
        {
            // An unescaped '&', '=' or '#' would split or truncate the query string.
            var handler = new CapturingHandler(EmptyPage);
            using var api = CreateInitializedApi(handler);

            await api.GetAppReviewsAsync(
                "a b&c=d",
                filter: "reviewText eq 'x&y=z#w'",
                ct: TestContext.CancellationToken);

            var query = HttpUtility.ParseQueryString(handler.RequestUris.Single().Query);
            query.AllKeys.Should().Equal("applicationId", "filter");
            query["applicationId"].Should().Be("a b&c=d");
            query["filter"].Should().Be("reviewText eq 'x&y=z#w'");
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldFormatDatesIndependentlyOfTheCurrentCulture()
        {
            // th-TH defaults to the Thai Buddhist calendar, which would render 2024 as 2567.
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");

                var handler = new CapturingHandler(EmptyPage);
                using var api = CreateInitializedApi(handler);

                await api.GetAppReviewsAsync("9NBLGGH0MTF4", startDate: new DateOnly(2024, 1, 31), ct: TestContext.CancellationToken);

                HttpUtility.ParseQueryString(handler.RequestUris.Single().Query)["startDate"].Should().Be("2024-01-31");
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldCarryTheStatusOfAFailureWithABody()
        {
            // The analytics API's authorization failures have a body, so without the status
            // on the exception a caller cannot tell them apart from any other error.
            var handler = new CapturingHandler("""{"error":"User Unauthorized due to AMS call failure."}""", HttpStatusCode.Unauthorized);
            using var api = CreateInitializedApi(handler);

            var act = async () => await api.GetAppReviewsAsync("9ZZZZZZZZZZZ", ct: TestContext.CancellationToken);

            var error = (await act.Should().ThrowAsync<MSStoreException>()).Which;
            error.Should().NotBeOfType<MSStoreHttpException>();
            error.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [TestMethod]
        public async Task GetAppReviewsAsyncShouldReadTheAnalyticsResponseShape()
        {
            // Field names as the service sends them: camelCase, the review id as "id", the title
            // as "reviewTitle", fields omitted entirely rather than null, an undocumented
            // "siloId", and a US-format date string rather than ISO-8601.
            const string payload = """
                {
                  "Value": [
                    { "date": "6/22/2016 6:55:33 PM", "applicationId": "9NBLGGH0MTF4", "market": "BR", "rating": 1, "reviewerName": "Elayne", "reviewTitle": "Tosco", "reviewText": "Não cumpre o que promete", "id": "5c92b8e3-fe2c-4e4c-8b2f-000000000001", "siloId": null },
                    { "date": "1/27/2016 11:51:59 AM", "rating": 5, "id": "5230c1c2-fe2c-4e4c-8b2f-000000000002" }
                  ],
                  "@nextLink": "reviews?applicationId=9NBLGGH0MTF4&top=2&skip=2",
                  "TotalCount": 2
                }
                """;
            using var api = CreateInitializedApi(new CapturingHandler(payload));

            var page = await api.GetAppReviewsAsync("9NBLGGH0MTF4", ct: TestContext.CancellationToken);

            page.TotalCount.Should().Be(2);
            page.Value.Should().HaveCount(2);

            // The service names it "@nextLink", which the naming policy cannot produce.
            page.NextLink.Should().Be("reviews?applicationId=9NBLGGH0MTF4&top=2&skip=2");

            var first = page.Value![0];
            first.Id.Should().Be("5c92b8e3-fe2c-4e4c-8b2f-000000000001");
            first.Date.Should().Be("6/22/2016 6:55:33 PM");
            first.ReviewTitle.Should().Be("Tosco");
            first.ReviewText.Should().Be("Não cumpre o que promete");
            first.Rating.Should().Be(1);

            var sparse = page.Value[1];
            sparse.Id.Should().Be("5230c1c2-fe2c-4e4c-8b2f-000000000002");
            sparse.ReviewerName.Should().BeNull();
            sparse.ReviewText.Should().BeNull();
            sparse.Market.Should().BeNull();
        }

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

        /// <summary>
        /// Builds an API whose requests go to <paramref name="handler"/>.
        /// </summary>
        /// <remarks>
        /// InitAsync always acquires a real token from Entra, so the client it would create is
        /// planted directly. MSStore.API is strong-named in Release builds, which rules out
        /// InternalsVisibleTo for this unsigned test assembly.
        /// </remarks>
        /// <param name="handler">The handler that receives every request.</param>
        /// <returns>An API ready to make requests.</returns>
        private static StorePackagedAPI CreateInitializedApi(HttpMessageHandler handler)
        {
            var api = CreateApi();

            var token = new AuthenticationResult(
                accessToken: "fake-token",
                isExtendedLifeTimeToken: false,
                uniqueId: null,
                expiresOn: DateTimeOffset.UtcNow.AddHours(1),
                extendedExpiresOn: DateTimeOffset.UtcNow.AddHours(1),
                tenantId: null,
                account: null,
                idToken: null,
                scopes: [],
                correlationId: Guid.Empty);

            var client = new SubmissionClient(token, "https://manage.devcenter.microsoft.com", new HttpClient(handler));

            var field = typeof(StorePackagedAPI).GetField("_devCenterClient", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("StorePackagedAPI no longer has a _devCenterClient field; update this test seam.");
            field.SetValue(api, client);

            return api;
        }

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

        private sealed class CapturingHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
        {
            public List<Uri> RequestUris { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUris.Add(request.RequestUri!);

                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
