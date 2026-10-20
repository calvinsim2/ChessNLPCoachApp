using ChessNLPCoachApp.Services;
using FakeItEasy;
using System.Net;
using System.Text.Json;

namespace ChessNLPCoachAppTest.ChessNLPCoachApp.Services
{
    public sealed class LangflowClientTest
    {
        private const string BaseUrlMock = "http://localhost:7860";
        private const string FlowIdMock = "flow-id";
        private const string ApiKeyMock = "api-key";
        private const string EngineAnalysisMock = "Move 1. e4: best move.";

        private readonly HttpClient _httpClientMock = A.Fake<HttpClient>();
        private readonly LangflowClient _langflowClient;

        public LangflowClientTest()
        {
            _langflowClient = new LangflowClient(_httpClientMock, BaseUrlMock, FlowIdMock, ApiKeyMock);
        }

        [Fact]
        public async Task AskLangFlowAsync_Positive_ValidResponse_ShouldReturnMessageText()
        {
            // Arrange
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"outputs":[{"outputs":[{"results":{"message":{"text":"e4 controls the centre."}}}]}]}""")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act
            string result = await _langflowClient.AskLangFlowAsync(EngineAnalysisMock);

            // Assert
            Assert.Equal("e4 controls the centre.", result);
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task AskLangFlowAsync_Positive_BaseUrlWithTrailingSlash_ShouldPostToRunEndpointWithApiKeyHeader()
        {
            // Arrange
            LangflowClient langflowClient = new(_httpClientMock, BaseUrlMock + "/", FlowIdMock, ApiKeyMock);
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"outputs":[{"outputs":[{"results":{"message":{"text":"ok"}}}]}]}""")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act
            await langflowClient.AskLangFlowAsync(EngineAnalysisMock);

            // Assert
            A.CallTo(() => _httpClientMock.SendAsync(
                    A<HttpRequestMessage>.That.Matches(request =>
                        request.Method == HttpMethod.Post
                        && request.RequestUri == new Uri("http://localhost:7860/api/v1/run/flow-id")
                        && request.Headers.GetValues("x-api-key").Single() == ApiKeyMock),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task AskLangFlowAsync_Positive_CancellationTokenProvided_ShouldPassTokenToHttpClient()
        {
            // Arrange
            using CancellationTokenSource cancellationTokenSource = new();
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"outputs":[{"outputs":[{"results":{"message":{"text":"ok"}}}]}]}""")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act
            await _langflowClient.AskLangFlowAsync(EngineAnalysisMock, cancellationTokenSource.Token);

            // Assert
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, cancellationTokenSource.Token))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_MessageTextIsNull_ShouldReturnRawResponse()
        {
            // Arrange
            string rawResponseMock = """{"outputs":[{"outputs":[{"results":{"message":{"text":null}}}]}]}""";
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent(rawResponseMock)
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act
            string result = await _langflowClient.AskLangFlowAsync(EngineAnalysisMock);

            // Assert
            Assert.Equal(rawResponseMock, result);
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_NonSuccessStatusCode_ShouldThrowWithStatusAndBody()
        {
            // Arrange
            using HttpResponseMessage responseMock = new(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("flow crashed")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act
            Exception exception = await Assert.ThrowsAsync<Exception>(
                () => _langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("LangFlow returned 500: flow crashed", exception.Message);
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_ResponseMissingExpectedProperty_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"outputs":[{"outputs":[{"results":{}}]}]}""")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act + Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _langflowClient.AskLangFlowAsync(EngineAnalysisMock));
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_ResponseIsNotJson_ShouldThrowJsonException()
        {
            // Arrange
            using HttpResponseMessage responseMock = new(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>not json</html>")
            };
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Returns(responseMock);

            // Act + Assert
            await Assert.ThrowsAnyAsync<JsonException>(
                () => _langflowClient.AskLangFlowAsync(EngineAnalysisMock));
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_HttpClientThrows_ShouldPropagateException()
        {
            // Arrange
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .Throws(new HttpRequestException("Connection refused"));

            // Act
            HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("Connection refused", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AskLangFlowAsync_Negative_BaseUrlNotConfigured_ShouldThrowInvalidOperationException(string? baseUrl)
        {
            // Arrange
            LangflowClient langflowClient = new(_httpClientMock, baseUrl, FlowIdMock, ApiKeyMock);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("LANGFLOW_BASE_URL has not been configured in the .env file.", exception.Message);
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AskLangFlowAsync_Negative_FlowIdNotConfigured_ShouldThrowInvalidOperationException(string? flowId)
        {
            // Arrange
            LangflowClient langflowClient = new(_httpClientMock, BaseUrlMock, flowId, ApiKeyMock);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("LANGFLOW_FLOW_ID has not been configured in the .env file.", exception.Message);
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AskLangFlowAsync_Negative_ApiKeyNotConfigured_ShouldThrowInvalidOperationException(string? apiKey)
        {
            // Arrange
            LangflowClient langflowClient = new(_httpClientMock, BaseUrlMock, FlowIdMock, apiKey);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("LANGFLOW_API_KEY has not been configured in the .env file.", exception.Message);
            A.CallTo(() => _httpClientMock.SendAsync(A<HttpRequestMessage>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AskLangFlowAsync_Negative_NothingConfigured_ShouldReportBaseUrlFirst()
        {
            // Arrange
            LangflowClient langflowClient = new(_httpClientMock, null, null, null);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => langflowClient.AskLangFlowAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("LANGFLOW_BASE_URL has not been configured in the .env file.", exception.Message);
        }
    }
}
