using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Interfaces
{
    /// <summary>
    /// Analyses the moves of a chess game: evaluates each one with Stockfish, scores it,
    /// and asks Langflow for a human-readable coaching explanation.
    /// </summary>
    public interface IMoveAnalysisService
    {
        /// <summary>
        /// Analyses every move of a game, in order.
        /// </summary>
        /// <param name="moves">The game's moves as parsed from the PGN, in the order they were played.</param>
        /// <returns>One <see cref="MoveAnalysis"/> per move, in the same order as <paramref name="moves"/>.</returns>
        Task<List<MoveAnalysis>> AnalyseGameAsync(IReadOnlyList<PgnMoveContext> moves, 
                                                  Func<AnalysisProgress, Task>? progressCallback = null);
    }
}
