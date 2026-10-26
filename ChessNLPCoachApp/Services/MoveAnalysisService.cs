using ChessNLPCoachApp.Helpers;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Services
{
    /// <summary>
    /// Default <see cref="IMoveAnalysisService"/>. For each move it gets Stockfish's evaluation,
    /// scores the move, builds the board facts and the coaching prompt, then asks the LLM
    /// coaching client (Azure AI Foundry by default) for the explanation.
    /// </summary>
    public class MoveAnalysisService : IMoveAnalysisService
    {
        // How many of Stockfish's top lines to request for each position.
        private const int TopLineCount = 3;

        private readonly IStockfishClient _stockfishClient;
        private readonly IFoundryChessCoachClient _foundryChessCoachClient;
        private readonly ILangflowClient _langflowClient;
        private readonly int _depth;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="stockfishClient">Client used to evaluate positions with Stockfish.</param>
        /// <param name="foundryChessCoachClient">
        /// Client used to get move explanations from the hosted Foundry model.
        /// </param>
        /// <param name="langflowClient">
        /// Optional local client for testing prompts against a Langflow flow. Not called by
        /// default; see <see cref="AnalyseMoveAsync"/>.
        /// </param>
        /// <param name="configuration">
        /// App configuration. <c>Stockfish:Depth</c> sets the search depth for every analysis.
        /// </param>
        public MoveAnalysisService(IStockfishClient stockfishClient,
                                   IFoundryChessCoachClient foundryChessCoachClient,
                                   ILangflowClient langflowClient,
                                   IConfiguration configuration)
        {
            _stockfishClient = stockfishClient;
            _foundryChessCoachClient = foundryChessCoachClient;
            _langflowClient = langflowClient;
            _depth = configuration.GetValue<int>("Stockfish:Depth");
        }

        /// <inheritdoc/>
        public async Task<List<MoveAnalysis>> AnalyseGameAsync(IReadOnlyList<PgnMoveContext> moves,
                                                               Func<AnalysisProgress, Task>? progressCallback = null)
        {
            await _stockfishClient.StartAsync();

            List<MoveAnalysis> analyses = new List<MoveAnalysis>();

            int totalPlys = moves.Count;

            int completedPlys = 0;

            foreach (PgnMoveContext move in moves)
            {
                MoveAnalysis analysedMove = await AnalyseMoveAsync(move);
                analyses.Add(analysedMove);

                completedPlys++;

                // Report progress if a callback was provided.
                if (progressCallback != null)
                {
                    AnalysisProgress progress = new AnalysisProgress(CompletedPlys: completedPlys,
                                                                     TotalPlys: totalPlys,
                                                                     San: move.San);

                    await progressCallback(progress);
                }
            }

            return analyses;
        }

        /// <summary>
        /// Analyses one move from start to finish.
        /// </summary>
        /// <param name="move">The move as parsed from the PGN.</param>
        /// <returns>The complete analysis of the move, including the LLM's explanation.</returns>
        private async Task<MoveAnalysis> AnalyseMoveAsync(PgnMoveContext move)
        {
            MoveEvaluation evaluation = await EvaluateWithStockfishAsync(move);

            MoveScore score = MoveClassifier.Score(evaluation);

            MoveFacts facts = MoveFactsBuilder.Build(move, evaluation);

            // ==============================================
            // BUILD DATA FOR NLP AI
            // ==============================================

            string engineAnalysis = CoachingPromptBuilder.Build(move, score, facts);

            // Foundry is the default explanation provider.
            // For local prompt testing, swap to Langflow:
            //string explanation = await _langflowClient.AskLangFlowAsync(engineAnalysis);
            string explanation = await _foundryChessCoachClient.ExplainAsync(engineAnalysis);

            return new MoveAnalysis(Move: move,
                                    BestLines: evaluation.BestLines,
                                    PlayedMoveLine: evaluation.PlayedMoveLine,
                                    PlayedMoveRank: evaluation.PlayedMoveRank,
                                    CentipawnLoss: score.CentipawnLoss,
                                    Quality: score.Quality,
                                    Explanation: explanation);
        }

        /// <summary>
        /// Gets Stockfish's top lines from the position before the move, and Stockfish's line
        /// for the move that was actually played.
        /// </summary>
        /// <remarks>
        /// If the played move is already one of the top lines, that line is reused. Otherwise
        /// Stockfish runs a second search limited to the played move. Both searches start from
        /// the same position, so their scores can be compared directly.
        /// </remarks>
        /// <param name="move">The move as parsed from the PGN.</param>
        /// <returns>Stockfish's evaluation of the move.</returns>
        private async Task<MoveEvaluation> EvaluateWithStockfishAsync(PgnMoveContext move)
        {
            // ==============================================
            // STOCKFISH'S TOP 3 MOVES
            // ==============================================

            List<EngineLine> bestLines =
                await _stockfishClient.AnalyseAsync(
                    fen: move.FenBeforeMove,
                    depth: _depth,
                    multiPv: TopLineCount);

            // ==============================================
            // CHECK WHETHER PLAYER'S MOVE IS ALREADY
            // INSIDE STOCKFISH'S TOP 3
            // ==============================================

            EngineLine? matchingCandidate =
                bestLines.FirstOrDefault(
                    line =>
                        string.Equals(
                            line.RootMoveUci,
                            move.Uci,
                            StringComparison.OrdinalIgnoreCase));

            // ==============================================
            // GET PLAYER'S MOVE ANALYSIS AND GET THE SINGLE RESULT
            // ==============================================

            EngineLine playedMoveLine;

            if (matchingCandidate != null)
            {
                playedMoveLine =
                    matchingCandidate;
            }
            else
            {
                List<EngineLine> playedMoveLines =
                    await _stockfishClient.AnalyseAsync(
                        fen: move.FenBeforeMove,
                        depth: _depth,
                        multiPv: 1,
                        searchMove: move.Uci);

                playedMoveLine = playedMoveLines.First();
            }

            return new MoveEvaluation(BestLines: bestLines,
                                      PlayedMoveLine: playedMoveLine,
                                      PlayedMoveRank: matchingCandidate?.MultiPv);
        }
    }
}
