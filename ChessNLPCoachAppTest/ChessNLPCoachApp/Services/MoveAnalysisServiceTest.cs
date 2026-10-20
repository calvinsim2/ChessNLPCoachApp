using ChessNLPCoachApp.Enums;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Records;
using ChessNLPCoachApp.Services;
using FakeItEasy;
using Microsoft.Extensions.Configuration;

namespace ChessNLPCoachAppTest.ChessNLPCoachApp.Services
{
    public sealed class MoveAnalysisServiceTest
    {
        private const int DepthMock = 12;

        private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private const string AfterE4Fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        private const string AfterE5Fen = "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2";
        private const string AfterA3Fen = "rnbqkbnr/pppppppp/8/8/8/P7/1PPPPPPP/RNBQKBNR b KQkq - 0 1";

        private readonly IStockfishClient _stockfishClientMock = A.Fake<IStockfishClient>();
        private readonly IFoundryChessCoachClient _foundryChessCoachClientMock = A.Fake<IFoundryChessCoachClient>();
        private readonly ILangflowClient _langflowClientMock = A.Fake<ILangflowClient>();
        private readonly MoveAnalysisService _moveAnalysisService;

        public MoveAnalysisServiceTest()
        {
            // Real in-memory configuration: GetValue<int> is an extension method and cannot be faked.
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Stockfish:Depth"] = DepthMock.ToString()
                })
                .Build();

            _moveAnalysisService = new MoveAnalysisService(
                _stockfishClientMock,
                _foundryChessCoachClientMock,
                _langflowClientMock,
                configuration);
        }

        [Fact]
        public async Task AnalyseGameAsync_Positive_PlayedMoveIsTopLine_ShouldReuseLineAndReturnBestMove()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "e4", "e2e4", StartFen, AfterE4Fen);
            List<EngineLine> bestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", 30, "e2e4 e7e5", 100, 850, 50),
                new EngineLine(2, DepthMock, "cp", 25, "d2d4 d7d5", 90, 860, 50),
                new EngineLine(3, DepthMock, "cp", 20, "g1f3 d7d5", 80, 870, 50)
            };
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null)).Returns(bestLinesMock);
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .Returns("e4 is the best move.");

            // Act
            List<MoveAnalysis> result = await _moveAnalysisService.AnalyseGameAsync(new[] { moveMock });

            // Assert
            MoveAnalysis analysis = Assert.Single(result);
            Assert.Equal(moveMock, analysis.Move);
            Assert.Equal(bestLinesMock, analysis.BestLines);
            Assert.Equal(bestLinesMock[0], analysis.PlayedMoveLine);
            Assert.Equal(1, analysis.PlayedMoveRank);
            Assert.Equal(0, analysis.CentipawnLoss);
            Assert.Equal(MoveQuality.Best, analysis.Quality);
            Assert.Equal("e4 is the best move.", analysis.Explanation);
            A.CallTo(() => _stockfishClientMock.StartAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 1, "e2e4")).MustNotHaveHappened();
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _langflowClientMock.AskLangFlowAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AnalyseGameAsync_Positive_PlayedMoveOutsideTopLines_ShouldRunSearchLimitedToPlayedMove()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "a3", "a2a3", StartFen, AfterA3Fen);
            List<EngineLine> bestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", 30, "e2e4 e7e5", 100, 850, 50),
                new EngineLine(2, DepthMock, "cp", 25, "d2d4 d7d5", 90, 860, 50),
                new EngineLine(3, DepthMock, "cp", 20, "g1f3 d7d5", 80, 870, 50)
            };
            EngineLine playedMoveLineMock = new(1, DepthMock, "cp", 0, "a2a3 e7e5", 60, 880, 60);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null)).Returns(bestLinesMock);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 1, "a2a3"))
                .Returns(new List<EngineLine> { playedMoveLineMock });
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .Returns("a3 is passive.");

            // Act
            List<MoveAnalysis> result = await _moveAnalysisService.AnalyseGameAsync(new[] { moveMock });

            // Assert: expected points 0.525 -> 0.500 is a 0.025 loss, which is Good.
            MoveAnalysis analysis = Assert.Single(result);
            Assert.Equal(playedMoveLineMock, analysis.PlayedMoveLine);
            Assert.Null(analysis.PlayedMoveRank);
            Assert.Equal(30, analysis.CentipawnLoss);
            Assert.Equal(MoveQuality.Good, analysis.Quality);
            Assert.Equal("a3 is passive.", analysis.Explanation);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 1, "a2a3"))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task AnalyseGameAsync_Positive_SeveralMoves_ShouldReturnAnalysesInOrderAndStartEngineOnce()
        {
            // Arrange
            PgnMoveContext whiteMoveMock = new(1, 1, "White", "e4", "e2e4", StartFen, AfterE4Fen);
            PgnMoveContext blackMoveMock = new(2, 1, "Black", "e5", "e7e5", AfterE4Fen, AfterE5Fen);
            List<EngineLine> whiteBestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", 30, "e2e4 e7e5", 100, 850, 50)
            };
            List<EngineLine> blackBestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", -30, "e7e5 g1f3", 50, 850, 100)
            };
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null)).Returns(whiteBestLinesMock);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(AfterE4Fen, DepthMock, 3, null)).Returns(blackBestLinesMock);
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .Returns("Good move.");

            // Act
            List<MoveAnalysis> result = await _moveAnalysisService.AnalyseGameAsync(new[] { whiteMoveMock, blackMoveMock });

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(whiteMoveMock, result[0].Move);
            Assert.Equal(blackMoveMock, result[1].Move);
            A.CallTo(() => _stockfishClientMock.StartAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .MustHaveHappenedTwiceExactly();
        }

        [Fact]
        public async Task AnalyseGameAsync_Negative_NoMoves_ShouldReturnEmptyListWithoutAnalysing()
        {
            // Act
            List<MoveAnalysis> result = await _moveAnalysisService.AnalyseGameAsync(Array.Empty<PgnMoveContext>());

            // Assert
            Assert.Empty(result);
            A.CallTo(() => _stockfishClientMock.StartAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(A<string>._, A<int>._, A<int>._, A<string?>._))
                .MustNotHaveHappened();
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AnalyseGameAsync_Negative_EngineFailsToStart_ShouldPropagateExceptionWithoutAnalysing()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "e4", "e2e4", StartFen, AfterE4Fen);
            A.CallTo(() => _stockfishClientMock.StartAsync())
                .Throws(new InvalidOperationException("Stockfish executable not found."));

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _moveAnalysisService.AnalyseGameAsync(new[] { moveMock }));

            // Assert
            Assert.Equal("Stockfish executable not found.", exception.Message);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(A<string>._, A<int>._, A<int>._, A<string?>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AnalyseGameAsync_Negative_EngineAnalysisFails_ShouldPropagateExceptionWithoutAskingCoach()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "e4", "e2e4", StartFen, AfterE4Fen);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null))
                .Throws(new TimeoutException("Stockfish did not respond."));

            // Act
            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(
                () => _moveAnalysisService.AnalyseGameAsync(new[] { moveMock }));

            // Assert
            Assert.Equal("Stockfish did not respond.", exception.Message);
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AnalyseGameAsync_Negative_PlayedMoveSearchReturnsNoLines_ShouldThrowInvalidOperationException()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "a3", "a2a3", StartFen, AfterA3Fen);
            List<EngineLine> bestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", 30, "e2e4 e7e5", 100, 850, 50)
            };
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null)).Returns(bestLinesMock);
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 1, "a2a3"))
                .Returns(new List<EngineLine>());

            // Act + Assert: the played-move search must return at least one line.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _moveAnalysisService.AnalyseGameAsync(new[] { moveMock }));
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task AnalyseGameAsync_Negative_CoachFails_ShouldPropagateException()
        {
            // Arrange
            PgnMoveContext moveMock = new(1, 1, "White", "e4", "e2e4", StartFen, AfterE4Fen);
            List<EngineLine> bestLinesMock = new()
            {
                new EngineLine(1, DepthMock, "cp", 30, "e2e4 e7e5", 100, 850, 50)
            };
            A.CallTo(() => _stockfishClientMock.AnalyseAsync(StartFen, DepthMock, 3, null)).Returns(bestLinesMock);
            A.CallTo(() => _foundryChessCoachClientMock.ExplainAsync(A<string>._, A<CancellationToken>._))
                .Throws(new InvalidOperationException("Foundry returned an empty explanation."));

            // Act
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _moveAnalysisService.AnalyseGameAsync(new[] { moveMock }));

            // Assert
            Assert.Equal("Foundry returned an empty explanation.", exception.Message);
        }
    }
}
