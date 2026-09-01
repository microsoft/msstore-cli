// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using MSStore.API;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class StoreAPIFactoryUnitTests
    {
        private static readonly Guid ClientId = new("3F0BCAEF-6334-48CF-837F-81CB0F1F2C45");

        private Mock<IConfigurationManager<Configurations>> _configurationManager = null!;
        private Mock<ICredentialManager> _credentialManager = null!;
        private StoreAPIFactory _factory = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Initialize()
        {
            _configurationManager = new Mock<IConfigurationManager<Configurations>>();
            _credentialManager = new Mock<ICredentialManager>();

            _factory = new StoreAPIFactory(
                _configurationManager.Object,
                _credentialManager.Object,
                new Mock<IHttpClientFactory>().Object,
                new Mock<ILogger<StoreAPI>>().Object);
        }

        private static Configurations ConfigWithNoCertificate() => new()
        {
            SellerId = 1,
            TenantId = new Guid("41261775-DB6D-4B44-9A36-7EB8565C7D22"),
            ClientId = ClientId
        };

        [TestMethod]
        public async Task CreateAsyncShouldReadTheSecretFromTheCredentialStore()
        {
            _credentialManager
                .Setup(x => x.ReadCredential(ClientId.ToString()))
                .Returns(string.Empty);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.CreateAsync(ConfigWithNoCertificate(), TestContext.CancellationToken));

            _credentialManager.Verify(x => x.ReadCredential(ClientId.ToString()), Times.Once);
        }

        [TestMethod]
        public async Task CreateWithSecretAsyncShouldNeverReadTheCredentialStore()
        {
            // The whole point of this overload: a candidate configuration can be validated without the credential
            // store having been staged with (or emptied of) anything first.
            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.CreateWithSecretAsync(ConfigWithNoCertificate(), null, TestContext.CancellationToken));

            _credentialManager.Verify(x => x.ReadCredential(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateWithSecretAsyncShouldRequireAClientId()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.CreateWithSecretAsync(new Configurations(), "secret", TestContext.CancellationToken));
        }

        [TestMethod]
        public async Task CreateWithSecretAsyncShouldRequireAConfiguration()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _factory.CreateWithSecretAsync(null!, "secret", TestContext.CancellationToken));
        }
    }
}
