namespace ChessNLPCoachApp.Records
{
    /// <summary>
    /// Deterministic, human-readable board facts about one played move, generated from the
    /// position itself rather than by the language model.
    /// </summary>
    /// <remarks>
    /// These facts are given to Langflow as the authoritative description of what happened
    /// on the board, so the model explains real facts instead of inventing them.
    /// </remarks>
    /// <param name="PlayedMoveDescription">Description of the move that was played.</param>
    /// <param name="BestMoveDescription">Description of Stockfish's best move from the same position.</param>
    /// <param name="BestMoveTacticalFact">
    /// What Stockfish's best move would have achieved immediately, e.g. a capture or a threat.
    /// </param>
    /// <param name="OpponentBestReplyDescription">
    /// Description of the opponent's best reply to the played move, or a fallback message
    /// if Stockfish's line contains no reply.
    /// </param>
    /// <param name="TacticalConsequence">
    /// What the opponent's best reply achieves immediately, or a fallback message
    /// if there is no reply.
    /// </param>
    public sealed record MoveFacts(
        string PlayedMoveDescription,
        string BestMoveDescription,
        string BestMoveTacticalFact,
        string OpponentBestReplyDescription,
        string TacticalConsequence);
}
