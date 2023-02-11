// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Identity.Client;
using Moq.Protected;
using MSStore.CLI.Services.Graph;
using MSStore.CLI.Services.TokenManager;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class GraphClientTests
    {
        private Mock<ITokenManager> mockTokenManager = null!;
        private IGraphClient _graphClient = null!;
        private Mock<HttpMessageHandler> _httpMessageHandler = null!;

        [TestInitialize]
        public void Init()
        {
            _httpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(_httpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://graph.microsoft.com/")
            };
            var mockIHttpClientFactory = new Mock<IHttpClientFactory>();
            mockIHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var mockAccount = new Mock<IAccount>();
            mockAccount
                .Setup(a => a.Username)
                .Returns("testUserName@fakedomain.com");
            mockAccount
                .Setup(a => a.HomeAccountId)
                .Returns(new AccountId("id", "123", BaseCommandLineTest.DefaultOrganization.Id.ToString()));
            mockTokenManager = new Mock<ITokenManager>();
            mockTokenManager
                .Setup(x => x.CurrentUser)
                .Returns((IAccount?)null);
            mockTokenManager
                .Setup(x => x.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAuthenticationResult("MY_ACCESS_TOKEN"));
            mockTokenManager
                .Setup(x => x.SelectAccountAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    mockTokenManager
                        .Setup(x => x.CurrentUser)
                        .Returns(mockAccount.Object);
                });

            _graphClient = new GraphClient(mockTokenManager.Object, mockIHttpClientFactory.Object);
        }

        private static AuthenticationResult CreateAuthenticationResult(string accessToken) =>
            new AuthenticationResult(accessToken, true, null, DateTimeOffset.Now, DateTimeOffset.Now, string.Empty, null, null, null, Guid.Empty);

        private void SetupReturn(HttpResponseMessage httpResponseMessage)
        {
            _httpMessageHandler
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(httpResponseMessage)
               .Verifiable();
        }

        [TestMethod]
        public async Task GraphClientShouldReturnApps()
        {
            SetupReturn(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"value":[{ "id":"1987e350-be08-4d60-af6f-c1fe7002dd0c", "appId":"ddc3a30f-ade8-482c-b208-92dcbe589e6d", "displayName":"MSStoreCLIAccess - aaa" }]}"""),
            });

            var result = await _graphClient.GetAppsByDisplayNameAsync("mocked", CancellationToken.None);

            result.Should().NotBeNull();
            result.Value.Should().NotBeEmpty();
            result.Value?.FirstOrDefault()?.DisplayName.Should().Be("MSStoreCLIAccess - aaa");
        }

        [TestMethod]
        public async Task GraphClientShouldCreateApps()
        {
            SetupReturn(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"id":"1987e350-be08-4d60-af6f-c1fe7002dd0c", "appId":"ddc3a30f-ade8-482c-b208-92dcbe589e6d", "displayName":"MSStoreCLIAccess - aaa" }"""),
            });

            var result = await _graphClient.CreateAppAsync("mocked", CancellationToken.None);

            result.Should().NotBeNull();
            result.DisplayName.Should().Be("MSStoreCLIAccess - aaa");
        }
    }
}
