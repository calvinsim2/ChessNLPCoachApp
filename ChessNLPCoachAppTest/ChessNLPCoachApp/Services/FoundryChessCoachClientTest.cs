using ChessNLPCoachApp.Services;
using FakeItEasy;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace ChessNLPCoachAppTest.ChessNLPCoachApp.Services
{
    public sealed class FoundryChessCoachClientTest
    {
        private const string EngineAnalysisMock = "Move 1. e4: best move.";

        private readonly ChatClient _chatClientMock = A.Fake<ChatClient>();
        private readonly FoundryChessCoachClient _foundryChessCoachClient;

        public FoundryChessCoachClientTest()
        {
            _foundryChessCoachClient = new FoundryChessCoachClient(_chatClientMock);
        }

        [Fact]
        public async Task ExplainAsync_Positive_ModelReturnsText_ShouldReturnExplanation()
        {
            // Arrange
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                ChatFinishReason.Stop,
                new ChatMessageContent("e4 controls the centre."));
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            string result = await _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock);

            // Assert
            Assert.Equal("e4 controls the centre.", result);
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task ExplainAsync_Positive_ModelReturnsSeveralParts_ShouldConcatenateTextAndSkipNonTextParts()
        {
            // Arrange
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                ChatFinishReason.Stop,
                new ChatMessageContent(
                    ChatMessageContentPart.CreateTextPart("e4 controls "),
                    ChatMessageContentPart.CreateImagePart(new Uri("https://example.com/board.png")),
                    ChatMessageContentPart.CreateTextPart("the centre.")));
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            string result = await _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock);

            // Assert
            Assert.Equal("e4 controls the centre.", result);
        }

        [Fact]
        public async Task ExplainAsync_Positive_ValidRequest_ShouldSendSystemAndUserMessagesWithDeterministicOptionsAndToken()
        {
            // Arrange
            using CancellationTokenSource cancellationTokenSource = new();
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                ChatFinishReason.Stop,
                new ChatMessageContent("ok"));
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            await _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock, cancellationTokenSource.Token);

            // Assert
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>.That.Matches(messages =>
                        messages.Count() == 2
                        && messages.First() is SystemChatMessage
                        && messages.Last() is UserChatMessage
                        && messages.Last().Content[0].Text == EngineAnalysisMock),
                    A<ChatCompletionOptions>.That.Matches(options =>
                        options.Temperature == 0f && options.MaxOutputTokenCount == 350),
                    cancellationTokenSource.Token))
                .MustHaveHappenedOnceExactly();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExplainAsync_Negative_EngineAnalysisEmpty_ShouldThrowArgumentException(string? engineAnalysis)
        {
            // Act
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _foundryChessCoachClient.ExplainAsync(engineAnalysis!));

            // Assert
            Assert.Equal("engineAnalysis", exception.ParamName);
            Assert.StartsWith("Engine analysis cannot be empty.", exception.Message);
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Theory]
        [InlineData(ChatFinishReason.Length)]
        [InlineData(ChatFinishReason.ContentFilter)]
        [InlineData(ChatFinishReason.ToolCalls)]
        public async Task ExplainAsync_Negative_FinishReasonNotStop_ShouldThrowInvalidOperationException(ChatFinishReason finishReason)
        {
            // Arrange
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                finishReason,
                new ChatMessageContent("Truncated explanation"));
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal($"Model response ended with {finishReason}.", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExplainAsync_Negative_ModelReturnsBlankText_ShouldThrowInvalidOperationException(string text)
        {
            // Arrange
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                ChatFinishReason.Stop,
                new ChatMessageContent(text));
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("Foundry returned an empty explanation.", exception.Message);
        }

        [Fact]
        public async Task ExplainAsync_Negative_ModelReturnsNoContentParts_ShouldThrowInvalidOperationException()
        {
            // Arrange
            ClientResult<ChatCompletion> chatResultMock = CreateChatResult(
                ChatFinishReason.Stop,
                new ChatMessageContent());
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Returns(chatResultMock);

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("Foundry returned an empty explanation.", exception.Message);
        }

        [Fact]
        public async Task ExplainAsync_Negative_ChatClientThrows_ShouldPropagateException()
        {
            // Arrange
            A.CallTo(() => _chatClientMock.CompleteChatAsync(
                    A<IEnumerable<ChatMessage>>._, A<ChatCompletionOptions>._, A<CancellationToken>._))
                .Throws(new ClientResultException("Service unavailable"));

            // Act
            ClientResultException exception = await Assert.ThrowsAsync<ClientResultException>(
                () => _foundryChessCoachClient.ExplainAsync(EngineAnalysisMock));

            // Assert
            Assert.Equal("Service unavailable", exception.Message);
        }

        // Test-data helper only: builds the SDK result object the fake ChatClient returns.
        private static ClientResult<ChatCompletion> CreateChatResult(
            ChatFinishReason finishReason,
            ChatMessageContent content)
        {
            // The model factory overload chosen here is flagged as an evaluation API by the SDK.
#pragma warning disable OPENAI001
            ChatCompletion chatCompletion = OpenAIChatModelFactory.ChatCompletion(
                finishReason: finishReason,
                content: content);
#pragma warning restore OPENAI001

            return ClientResult.FromValue(chatCompletion, A.Fake<PipelineResponse>());
        }
    }
}
