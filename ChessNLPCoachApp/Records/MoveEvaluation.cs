namespace ChessNLPCoachApp.Records
{
    /// <summary>
    /// Stockfish's raw evaluation of one played move: the top lines from the position before
    /// the move, and the line for the move that was actually played.
    /// </summary>
    /// <remarks>
    /// Every line is searched from the same position (the FEN before the move), so all scores
    /// share the same side-to-move perspective and can be compared directly.
    /// </remarks>
    /// <param name="BestLines">
    /// Stockfish's top-ranked lines from the position before the move, ordered by
    /// <see cref="EngineLine.MultiPv"/> (best first). Always contains at least one line.
    /// </param>
    /// <param name="PlayedMoveLine">
    /// Stockfish's evaluation of the move that was actually played. If the move is one of the
    /// <paramref name="BestLines"/>, this is that same line.
    /// </param>
    /// <param name="PlayedMoveRank">
    /// The played move's rank among <paramref name="BestLines"/> (1 = best move),
    /// or <c>null</c> if it isn't one of the top lines.
    /// </param>
    public sealed record MoveEvaluation(
        IReadOnlyList<EngineLine> BestLines,
        EngineLine PlayedMoveLine,
        int? PlayedMoveRank)
    {
        /// <summary>
        /// Stockfish's best line (rank 1) from the position before the move.
        /// </summary>
        public EngineLine BestLine => BestLines[0];
    }
}
