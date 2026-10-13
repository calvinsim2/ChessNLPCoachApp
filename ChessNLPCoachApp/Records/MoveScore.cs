using ChessNLPCoachApp.Enums;

namespace ChessNLPCoachApp.Records
{
    /// <summary>
    /// How much a played move cost compared with Stockfish's best move, and the quality
    /// category that follows from it.
    /// </summary>
    /// <param name="CentipawnLoss">
    /// Evaluation lost compared with the best move, in centipawns (never negative).
    /// 0 if either line is a mate score rather than a centipawn score.
    /// </param>
    /// <param name="ExpectedPointLoss">
    /// Expected game score lost compared with the best move, from 0.0 to 1.0 (never negative).
    /// 0 if WDL data is missing.
    /// </param>
    /// <param name="Quality">Category assigned from the rank and <paramref name="ExpectedPointLoss"/>.</param>
    public sealed record MoveScore(
        int CentipawnLoss,
        double ExpectedPointLoss,
        MoveQuality Quality);
}
