// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MSStore.CLI.Services;
using MSStore.CLI.Services.CredentialManager;
using MSStore.CLI.Services.Translation;

namespace MSStore.CLI.UnitTests
{
    [TestClass]
    public class AzureAITranslatorServiceUnitTests
    {
        private const string FakeKey = "fake-translator-key";

        public TestContext TestContext { get; set; } = null!;

        private List<HttpRequestMessage> _requests = null!;
        private List<string> _requestBodies = null!;
        private Queue<HttpResponseMessage> _responses = null!;
        private Mock<ICredentialManager> _credentialManager = null!;
        private Mock<IConfigurationManager<Configurations>> _configurationManager = null!;
        private Mock<IEnvironmentInformationService> _environmentInformationService = null!;
        private Configurations _configurations = null!;

        [TestInitialize]
        public void Init()
        {
            _requests = [];
            _requestBodies = [];
            _responses = new Queue<HttpResponseMessage>();

            _credentialManager = new Mock<ICredentialManager>();
            _credentialManager
                .Setup(x => x.ReadCredential(AzureAITranslatorService.CredentialKeyName))
                .Returns(FakeKey);

            _configurations = new Configurations();
            _configurationManager = new Mock<IConfigurationManager<Configurations>>();
            _configurationManager
                .Setup(x => x.LoadAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _configurations);

            // Environment variables are read through this service rather than
            // System.Environment, so these tests never mutate process-wide state and cannot
            // be affected by whatever the developer happens to have set in their shell.
            _environmentInformationService = new Mock<IEnvironmentInformationService>();
        }

        [TestMethod]
        public async Task TranslateAsyncShouldReturnTranslatedTextAndDetectedLanguage()
        {
            EnqueueJson(HttpStatusCode.OK, """
                [{"detectedLanguage":{"language":"pt","score":1.0},"translations":[{"text":"A fantastic game","to":"en"}]}]
                """);

            var results = await CreateService().TranslateAsync(["Um jogo fantástico"], "en", TestContext.CancellationToken);

            results.Should().HaveCount(1);
            results[0]!.Text.Should().Be("A fantastic game");
            results[0]!.DetectedLanguage.Should().Be("pt");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldOmitFromParameterSoTheServiceAutoDetects()
        {
            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            var uri = _requests.Single().RequestUri!.ToString();
            uri.Should().Contain("api-version=3.0");
            uri.Should().Contain("to=en");
            uri.Should().NotContain("from=");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldSendTheDocumentedRequestBodyShape()
        {
            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            // A bare JSON array whose elements carry the text under the name the Translator
            // reference documents. Pinned so a change to the shared naming policy cannot
            // silently alter the wire format.
            var body = _requestBodies.Single();

            body.Should().StartWith("[").And.EndWith("]");
            body.Should().Contain("\"Text\":");
            body.Should().Contain("ol\\u00E1");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldNotSendRegionHeaderWhenNoRegionIsConfigured()
        {
            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Single().Headers.Contains("Ocp-Apim-Subscription-Region").Should().BeFalse();
            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Key").Single().Should().Be(FakeKey);
        }

        [TestMethod]
        public async Task TranslateAsyncShouldSendRegionHeaderWhenConfigured()
        {
            _configurations.TranslatorRegion = "westus2";
            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Region").Single().Should().Be("westus2");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldPreferEnvironmentVariablesOverStoredValues()
        {
            _environmentInformationService
                .Setup(x => x.GetEnvironmentVariable(AzureAITranslatorService.KeyEnvironmentVariable))
                .Returns("env-key");
            _environmentInformationService
                .Setup(x => x.GetEnvironmentVariable(AzureAITranslatorService.RegionEnvironmentVariable))
                .Returns("eastus");
            _configurations.TranslatorRegion = "westus2";

            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Key").Single().Should().Be("env-key");
            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Region").Single().Should().Be("eastus");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldTrimEnvironmentSuppliedCredentials()
        {
            // A key pasted or piped in often carries a trailing newline, which is not valid
            // in a header value and would otherwise throw before the request is sent.
            _environmentInformationService
                .Setup(x => x.GetEnvironmentVariable(AzureAITranslatorService.KeyEnvironmentVariable))
                .Returns("  env-key\n");
            _environmentInformationService
                .Setup(x => x.GetEnvironmentVariable(AzureAITranslatorService.RegionEnvironmentVariable))
                .Returns(" eastus \r\n");

            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Key").Single().Should().Be("env-key");
            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Region").Single().Should().Be("eastus");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldTrimStoredCredentials()
        {
            _credentialManager
                .Setup(x => x.ReadCredential(AzureAITranslatorService.CredentialKeyName))
                .Returns($"{FakeKey}\n");
            _configurations.TranslatorRegion = " westus2 ";

            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Key").Single().Should().Be(FakeKey);
            _requests.Single().Headers.GetValues("Ocp-Apim-Subscription-Region").Single().Should().Be("westus2");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldTreatWhitespaceOnlyKeyAsMissing()
        {
            _credentialManager
                .Setup(x => x.ReadCredential(It.IsAny<string>()))
                .Returns("   ");

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*MSSTORE_TRANSLATOR_KEY*set-translator-key*");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldSkipEmptyEntriesButKeepPositions()
        {
            EnqueueJson(HttpStatusCode.OK, """
                [{"translations":[{"text":"one","to":"en"}]},{"translations":[{"text":"two","to":"en"}]}]
                """);

            var results = await CreateService().TranslateAsync(["um", null, "   ", "dois"], "en", TestContext.CancellationToken);

            results.Should().HaveCount(4);
            results[0]!.Text.Should().Be("one");
            results[1].Should().BeNull();
            results[2].Should().BeNull();
            results[3]!.Text.Should().Be("two");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldNotCallTheServiceWhenEverythingIsEmpty()
        {
            var results = await CreateService().TranslateAsync([null, "  "], "en", TestContext.CancellationToken);

            results.Should().HaveCount(2);
            _requests.Should().BeEmpty();
        }

        [TestMethod]
        public async Task TranslateAsyncShouldSplitBatchesThatExceedTheElementLimit()
        {
            var texts = new List<string?>();
            for (var i = 0; i < AzureAITranslatorService.MaxElementsPerRequest + 5; i++)
            {
                texts.Add($"t{i}");
            }

            EnqueueJson(HttpStatusCode.OK, BuildTranslationsJson(AzureAITranslatorService.MaxElementsPerRequest));
            EnqueueJson(HttpStatusCode.OK, BuildTranslationsJson(5));

            var results = await CreateService().TranslateAsync(texts, "en", TestContext.CancellationToken);

            _requests.Should().HaveCount(2);
            results.Should().HaveCount(AzureAITranslatorService.MaxElementsPerRequest + 5);
            results.Should().AllSatisfy(r => r.Should().NotBeNull());
        }

        [TestMethod]
        public async Task TranslateAsyncShouldSplitBatchesThatExceedTheCharacterLimit()
        {
            var big = new string('a', (AzureAITranslatorService.MaxCharactersPerRequest / 2) + 1);

            EnqueueJson(HttpStatusCode.OK, BuildTranslationsJson(1));
            EnqueueJson(HttpStatusCode.OK, BuildTranslationsJson(1));

            await CreateService().TranslateAsync([big, big], "en", TestContext.CancellationToken);

            _requests.Should().HaveCount(2);
        }

        [TestMethod]
        public async Task TranslateAsyncShouldThrowWhenNoKeyIsConfigured()
        {
            _credentialManager
                .Setup(x => x.ReadCredential(It.IsAny<string>()))
                .Returns(string.Empty);

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*MSSTORE_TRANSLATOR_KEY*set-translator-key*");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldMapAuthenticationErrors()
        {
            // The service returns 'code' as a JSON number, not a string.
            EnqueueJson(HttpStatusCode.Unauthorized, """
                {"error":{"code":401001,"message":"The request is not authorized because credentials are missing or invalid."}}
                """);

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*rejected the credentials*");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldMapSpeechKeyError()
        {
            EnqueueJson(HttpStatusCode.Unauthorized, """
                {"error":{"code":401015,"message":"The credentials provided are for the Speech API."}}
                """);

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*Speech API*");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldMapQuotaExceededError()
        {
            EnqueueJson(HttpStatusCode.Forbidden, """
                {"error":{"code":403001,"message":"The operation isn't allowed because the subscription exceeded its free quota."}}
                """);

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*exceeded its free quota*");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldRetryThrottledRequests()
        {
            EnqueueJson(HttpStatusCode.TooManyRequests, """{"error":{"code":429001,"message":"Too many requests."}}""");
            EnqueueJson(HttpStatusCode.OK, """[{"translations":[{"text":"hi","to":"en"}]}]""");

            var results = await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            _requests.Should().HaveCount(2);
            results[0]!.Text.Should().Be("hi");
        }

        [TestMethod]
        public async Task TranslateAsyncShouldGiveUpAfterRepeatedThrottling()
        {
            for (var i = 0; i < 3; i++)
            {
                EnqueueJson(HttpStatusCode.TooManyRequests, """{"error":{"code":429001,"message":"Too many requests."}}""");
            }

            var act = async () => await CreateService().TranslateAsync(["olá"], "en", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*throttling*");
            _requests.Should().HaveCount(3);
        }

        [TestMethod]
        public async Task ResolveLanguageAsyncShouldReturnTheCanonicalCasing()
        {
            EnqueueJson(HttpStatusCode.OK, """
                {"translation":{"en":{"name":"English"},"pt-PT":{"name":"Portuguese (Portugal)"}}}
                """);

            var resolved = await CreateService().ResolveLanguageAsync("PT-pt", TestContext.CancellationToken);

            resolved.Should().Be("pt-PT");
        }

        [TestMethod]
        public async Task ResolveLanguageAsyncShouldNotAuthenticateTheLanguagesCall()
        {
            EnqueueJson(HttpStatusCode.OK, """{"translation":{"en":{"name":"English"}}}""");

            await CreateService().ResolveLanguageAsync("en", TestContext.CancellationToken);

            _requests.Single().Headers.Contains("Ocp-Apim-Subscription-Key").Should().BeFalse();
        }

        [TestMethod]
        public async Task ResolveLanguageAsyncShouldRejectUnsupportedLanguages()
        {
            EnqueueJson(HttpStatusCode.OK, """{"translation":{"en":{"name":"English"}}}""");

            var act = async () => await CreateService().ResolveLanguageAsync("zz", TestContext.CancellationToken);

            (await act.Should().ThrowAsync<TranslationException>())
                .WithMessage("*not a language supported*");
        }

        [TestMethod]
        public async Task ResolveLanguageAsyncShouldFallBackWhenTheLanguageListIsUnavailable()
        {
            EnqueueJson(HttpStatusCode.ServiceUnavailable, "{}");

            var resolved = await CreateService().ResolveLanguageAsync("pt", TestContext.CancellationToken);

            resolved.Should().Be("pt");
        }

        private static string BuildTranslationsJson(int count)
        {
            var items = Enumerable.Repeat("""{"translations":[{"text":"x","to":"en"}]}""", count);
            return $"[{string.Join(',', items)}]";
        }

        private void EnqueueJson(HttpStatusCode statusCode, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }

        private AzureAITranslatorService CreateService()
        {
            var handler = new StubHttpMessageHandler(_requests, _requestBodies, _responses);

            var httpClientFactory = new Mock<IHttpClientFactory>();
            httpClientFactory
                .Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler, disposeHandler: false)
                {
                    BaseAddress = new Uri("https://api.cognitive.microsofttranslator.com")
                });

            return new AzureAITranslatorService(
                httpClientFactory.Object,
                _credentialManager.Object,
                _configurationManager.Object,
                _environmentInformationService.Object,
                NullLogger<AzureAITranslatorService>.Instance);
        }

        private sealed class StubHttpMessageHandler(List<HttpRequestMessage> requests, List<string> requestBodies, Queue<HttpResponseMessage> responses) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // The body has to be read here: the caller disposes the request, and with it
                // the content, as soon as the send completes.
                requestBodies.Add(request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));

                requests.Add(request);

                return responses.Count > 0
                    ? responses.Dequeue()
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                    };
            }
        }
    }
}
