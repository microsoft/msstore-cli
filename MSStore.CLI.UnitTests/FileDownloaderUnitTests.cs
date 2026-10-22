// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using MSStore.CLI.Services;
using MSStore.CLI.Services.Http;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class FileDownloaderUnitTests
    {
        private string _tempDir = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void Initialize()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "msstore-filedownloader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_tempDir, true);
        }

        [DataRow("8.8.8.8", true)]
        [DataRow("2606:4700:4700::1111", true)]
        [DataRow("::ffff:8.8.8.8", true)]
        [DataRow("0.0.0.0", false)]
        [DataRow("10.1.2.3", false)]
        [DataRow("100.64.0.1", false)]
        [DataRow("127.0.0.1", false)]
        [DataRow("169.254.169.254", false)]
        [DataRow("172.16.0.1", false)]
        [DataRow("192.168.1.1", false)]
        [DataRow("224.0.0.1", false)]
        [DataRow("::", false)]
        [DataRow("::1", false)]
        [DataRow("::ffff:169.254.169.254", false)]
        [DataRow("fe80::1", false)]
        [DataRow("fd00:ec2::254", false)]
        [DataRow("ff02::1", false)]
        [TestMethod]
        public void IsPublicAddress(string address, bool expected)
        {
            Assert.AreEqual(expected, PublicHttpsPolicy.IsPublicAddress(IPAddress.Parse(address)));
        }

        [DataRow("https://example.com/icon.png", true)]
        [DataRow("http://example.com/icon.png", false)]
        [DataRow("file:///etc/passwd", false)]
        [DataRow("https://user:pass@example.com/icon.png", false)]
        [DataRow("/relative/icon.png", false)]
        [TestMethod]
        public void TryCreateUri(string url, bool expected)
        {
            Assert.AreEqual(expected, PublicHttpsPolicy.TryCreateUri(url, out _, out _));
        }

        [DataRow("https://localhost:1/icon.png")]
        [DataRow("https://127.0.0.1:1/icon.png")]
        [DataRow("https://169.254.169.254/latest/meta-data")]
        [DataRow("https://[::1]:1/icon.png")]
        [TestMethod]
        public async Task HandlerBlocksNonPublicAddresses(string url)
        {
            using var client = new HttpClient(PublicHttpsPolicy.CreateHandler());

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri(url), TestContext.CancellationToken));

            Assert.Contains("non-public IP address", ex.Message);
        }

        [TestMethod]
        public async Task DownloadsImage()
        {
            var handler = new FakeHandler(ImageResponse);
            var destination = Path.Combine(_tempDir, "icon.png");

            var result = await CreateDownloader(handler).DownloadAsync("https://example.com/icon.png", destination, new Progress<double>(), null, TestContext.CancellationToken);

            Assert.IsTrue(result);
            Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
            Assert.IsFalse(handler.Requests[0].Headers.Contains("MS-CorrelationId"));
        }

        [TestMethod]
        public async Task RejectsHttpUrlWithoutSendingRequest()
        {
            var handler = new FakeHandler(ImageResponse);

            var result = await CreateDownloader(handler).DownloadAsync("http://example.com/icon.png", Path.Combine(_tempDir, "icon.png"), new Progress<double>(), null, TestContext.CancellationToken);

            Assert.IsFalse(result);
            Assert.IsEmpty(handler.Requests);
        }

        private static FileDownloader CreateDownloader(HttpMessageHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory
                .Setup(f => f.CreateClient(FileDownloader.HttpClientName))
                .Returns(() => new HttpClient(handler, disposeHandler: false));
            return new FileDownloader(factory.Object);
        }

        private static HttpResponseMessage ImageResponse()
        {
            var content = new ByteArrayContent([1, 2, 3]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }

        private sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(respond());
            }
        }
    }
}
