using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Helpers
{
    /// <summary>
    /// Builds the deterministic board facts about a played move that are given to Langflow.
    /// </summary>
    public static class MoveFactsBuilder
    {
        /// <summary>
        /// Describes the played move, Stockfish's best move, and the opponent's best reply,
        /// along with the immediate tactical consequences of each.
        /// </summary>
        /// <param name="move">The move as parsed from the PGN, including the FENs before and after it.</param>
        /// <param name="evaluation">Stockfish's evaluation of the played move.</param>
        /// <returns>The board facts for the move.</returns>
        public static MoveFacts Build(PgnMoveContext move,
                                      MoveEvaluation evaluation)
        {
            string playedMoveDescription = ChessBoardAnalysisHelper.DescribeUciMove(move.FenBeforeMove, move.Uci);

            string bestMoveUci = evaluation.BestLine.RootMoveUci;

            string bestMoveDescription = ChessBoardAnalysisHelper.DescribeUciMove(move.FenBeforeMove, bestMoveUci);

            string bestMoveTacticalFact = ChessBoardAnalysisHelper.DescribeImmediateTacticalConsequence(move.FenBeforeMove,
                                                                                                        bestMoveUci);

            string opponentBestReplyUci = ChessBoardAnalysisHelper.GetOpponentBestReplyUci(evaluation.PlayedMoveLine);

            string opponentBestReplyDescription =
                string.IsNullOrWhiteSpace(opponentBestReplyUci) ? "No immediate reply available." :
                ChessBoardAnalysisHelper.DescribeUciMove(move.FenAfterMove, opponentBestReplyUci);

            string tacticalConsequence =
                string.IsNullOrWhiteSpace(opponentBestReplyUci) ?
                "No immediate tactical consequence was detected." :
                ChessBoardAnalysisHelper.DescribeImmediateTacticalConsequence(move.FenAfterMove, opponentBestReplyUci);

            return new MoveFacts(PlayedMoveDescription: playedMoveDescription,
                                 BestMoveDescription: bestMoveDescription,
                                 BestMoveTacticalFact: bestMoveTacticalFact,
                                 OpponentBestReplyDescription: opponentBestReplyDescription,
                                 TacticalConsequence: tacticalConsequence);
        }
    }
}
