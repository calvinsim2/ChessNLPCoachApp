using ChessNLPCoachApp.Enums;

namespace ChessNLPCoachApp.Records
{
    /// <summary>
    /// The complete analysis of one played move: the move itself, Stockfish's recommended
    /// alternatives, Stockfish's evaluation of the played move, and a human-readable
    /// explanation of why the move was good or bad.
    /// </summary>
    /// <param name="Move">The move as parsed from the PGN.</param>
    /// <param name="BestLines">
    /// Stockfish's top-ranked lines from the position before the move was played.
    /// </param>
    /// <param name="PlayedMoveLine">Stockfish's evaluation of the move that was actually played.</param>
    /// <param name="PlayedMoveRank">
    /// The played move's rank among <paramref name="BestLines"/> (1 = best move),
    /// or <c>null</c> if it isn't one of the top lines.
    /// </param>
    /// <param name="CentipawnLoss">
    /// Evaluation lost compared with Stockfish's best move, in centipawns.
    /// Higher means a more serious mistake.
    /// </param>
    /// <param name="Quality">Category assigned based on <paramref name="CentipawnLoss"/>.</param>
    /// <param name="Explanation">Human-readable explanation of why the move was good or bad.</param>
    public sealed record MoveAnalysis(
        PgnMoveContext Move,
        IReadOnlyList<EngineLine> BestLines,
        EngineLine PlayedMoveLine,
        int? PlayedMoveRank,
        int CentipawnLoss,
        MoveQuality Quality,
        string Explanation);
}
