using ChessNLPCoachApp.Enums;
using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Helpers
{
    /// <summary>
    /// Scores a played move against Stockfish's best move and assigns it a
    /// <see cref="MoveQuality"/> category.
    /// </summary>
    /// <remarks>
    /// Classification is based on expected-point loss (from WDL) rather than centipawn loss,
    /// because the same centipawn drop matters far more in a balanced position than in one
    /// that is already completely winning or lost.
    /// </remarks>
    public static class MoveClassifier
    {
        // Upper bounds of expected-point loss for each category.
        private const double ExcellentMaxLoss = 0.02;
        private const double GoodMaxLoss = 0.05;
        private const double InaccuracyMaxLoss = 0.10;
        private const double MistakeMaxLoss = 0.20;

        // Thresholds for detecting a missed winning opportunity.
        private const double StrongWinningExpectedPoints = 0.75;
        private const double OpportunityGoneExpectedPoints = 0.55;
        private const double MissMinLoss = 0.15;

        /// <summary>
        /// Calculates every score for a played move in one call.
        /// </summary>
        /// <param name="evaluation">Stockfish's evaluation of the played move.</param>
        /// <returns>The centipawn loss, expected-point loss and quality of the move.</returns>
        public static MoveScore Score(MoveEvaluation evaluation)
        {
            int centipawnLoss = CalculateCentipawnLoss(evaluation.BestLine, evaluation.PlayedMoveLine);

            double expectedPointLoss = CalculateExpectedPointLoss(evaluation.BestLine, evaluation.PlayedMoveLine);

            MoveQuality quality = ClassifyMove(evaluation.BestLine,
                                               evaluation.PlayedMoveLine,
                                               evaluation.PlayedMoveRank,
                                               expectedPointLoss);

            return new MoveScore(CentipawnLoss: centipawnLoss,
                                 ExpectedPointLoss: expectedPointLoss,
                                 Quality: quality);
        }

        /// <summary>
        /// Calculates how many centipawns the player lost compared with Stockfish's best move.
        /// </summary>
        /// <param name="bestLine">Stockfish's best line from the position before the move.</param>
        /// <param name="playedMoveLine">Stockfish's line for the move that was actually played.</param>
        /// <returns>
        /// The loss in centipawns (never negative), or 0 if either line is a mate score.
        /// </returns>
        public static int CalculateCentipawnLoss(EngineLine bestLine,
                                                 EngineLine playedMoveLine)
        {
            if (bestLine.ScoreType != "cp" ||
                playedMoveLine.ScoreType != "cp")
            {
                return 0;
            }

            // basically this returns -> How many centipawns did the player lose
            // compared with Stockfish's preferred move?
            int centipawnLoss =
                bestLine.ScoreValue -
                playedMoveLine.ScoreValue;

            return Math.Max(
                0,
                centipawnLoss);
        }

        /// <summary>
        /// Calculates how much expected game score the player lost compared with Stockfish's best move.
        /// </summary>
        /// <param name="bestLine">Stockfish's best line from the position before the move.</param>
        /// <param name="playedMoveLine">Stockfish's line for the move that was actually played.</param>
        /// <returns>
        /// The loss from 0.0 to 1.0 (never negative), or 0 if either line has no WDL data.
        /// </returns>
        public static double CalculateExpectedPointLoss(EngineLine bestLine,
                                                        EngineLine playedMoveLine)
        {
            if (bestLine.ExpectedPoints == null ||
                playedMoveLine.ExpectedPoints == null)
            {
                return 0;
            }

            double expectedPointLoss =
                bestLine.ExpectedPoints.Value -
                playedMoveLine.ExpectedPoints.Value;

            return Math.Max(
                0,
                expectedPointLoss);
        }

        /// <summary>
        /// Assigns a quality category to the played move.
        /// </summary>
        /// <param name="bestLine">Stockfish's best line from the position before the move.</param>
        /// <param name="playedMoveLine">Stockfish's line for the move that was actually played.</param>
        /// <param name="playedMoveRank">
        /// The played move's rank among Stockfish's top lines (1 = best move),
        /// or <c>null</c> if it isn't one of them.
        /// </param>
        /// <param name="expectedPointLoss">
        /// Result of <see cref="CalculateExpectedPointLoss"/> for the same two lines.
        /// </param>
        /// <returns>
        /// <see cref="MoveQuality.Best"/> for Stockfish's top move, <see cref="MoveQuality.Miss"/>
        /// for a missed winning opportunity, otherwise a category based on
        /// <paramref name="expectedPointLoss"/>.
        /// </returns>
        public static MoveQuality ClassifyMove(EngineLine bestLine,
                                               EngineLine playedMoveLine,
                                               int? playedMoveRank,
                                               double expectedPointLoss)
        {
            if (playedMoveRank == 1)
            {
                return MoveQuality.Best;
            }

            if (IsMissedOpportunity(
                    bestLine,
                    playedMoveLine,
                    expectedPointLoss))
            {
                return MoveQuality.Miss;
            }

            if (expectedPointLoss <= ExcellentMaxLoss)
            {
                return MoveQuality.Excellent;
            }

            if (expectedPointLoss <= GoodMaxLoss)
            {
                return MoveQuality.Good;
            }

            if (expectedPointLoss <= InaccuracyMaxLoss)
            {
                return MoveQuality.Inaccuracy;
            }

            if (expectedPointLoss <= MistakeMaxLoss)
            {
                return MoveQuality.Mistake;
            }

            return MoveQuality.Blunder;
        }

        /// <summary>
        /// Determines whether the player had a strong winning chance and let it slip with this move.
        /// </summary>
        /// <param name="bestLine">Stockfish's best line from the position before the move.</param>
        /// <param name="playedMoveLine">Stockfish's line for the move that was actually played.</param>
        /// <param name="expectedPointLoss">
        /// Result of <see cref="CalculateExpectedPointLoss"/> for the same two lines.
        /// </param>
        /// <returns>
        /// <c>true</c> if the best move kept a strong winning chance, the played move gave most
        /// of it away, and the loss was significant. <c>false</c> if WDL data is missing.
        /// </returns>
        public static bool IsMissedOpportunity(EngineLine bestLine,
                                               EngineLine playedMoveLine,
                                               double expectedPointLoss)
        {
            if (bestLine.ExpectedPoints == null ||
                playedMoveLine.ExpectedPoints == null)
            {
                return false;
            }

            bool hadStrongWinningOpportunity =
                bestLine.ExpectedPoints.Value >= StrongWinningExpectedPoints;

            bool winningOpportunityDisappeared =
                playedMoveLine.ExpectedPoints.Value <= OpportunityGoneExpectedPoints;

            bool evaluationChangedSignificantly =
                expectedPointLoss >= MissMinLoss;

            return
                hadStrongWinningOpportunity &&
                winningOpportunityDisappeared &&
                evaluationChangedSignificantly;
        }
    }
}
