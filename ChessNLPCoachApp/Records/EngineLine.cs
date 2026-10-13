namespace ChessNLPCoachApp.Records
{
    // =============================================================
    // STOCKFISH ANALYSIS MODEL
    // =============================================================

    // Record - designed primarily for objects whose main purpose is to hold data.
    // Record are particularly useful for cases - This collection of values represents one result.
    // vs Class - identity (class) versus data (record)
    // record has value-based equality - assuming if you have a record called 'Name', and you create two separate instances with the name 'Ali'.
    // when you try to equate them Record a == Record b, C# will return true. This is not the same like Class.

    /// <summary>
    /// One ranked line from Stockfish's analysis of a position (a single <c>multipv</c> result).
    /// </summary>
    /// <remarks>
    /// Scores and WDL values are stored exactly as Stockfish reports them, which is from the
    /// perspective of the side to move, not always White.
    /// </remarks>
    /// <param name="MultiPv">Rank of this line: 1 = best move, 2 = second best, and so on.</param>
    /// <param name="Depth">Search depth, in plies, at which Stockfish reported this line.</param>
    /// <param name="ScoreType">Evaluation kind: <c>"cp"</c> (centipawns) or <c>"mate"</c>.</param>
    /// <param name="ScoreValue">
    /// The evaluation number. For <c>"cp"</c>, 125 means +1.25 pawns for the side to move.
    /// For <c>"mate"</c>, 3 means the side to move mates in 3 (negative means it gets mated).
    /// </param>
    /// <param name="PrincipalVariation">
    /// Space-separated UCI moves Stockfish expects if both sides keep playing its best moves.
    /// </param>
    /// <param name="WdlWin">Win probability per mille (0–1000), or <c>null</c> if WDL wasn't reported.</param>
    /// <param name="WdlDraw">Draw probability per mille (0–1000), or <c>null</c> if WDL wasn't reported.</param>
    /// <param name="WdlLoss">Loss probability per mille (0–1000), or <c>null</c> if WDL wasn't reported.</param>
    public sealed record EngineLine(
        int MultiPv,
        int Depth,
        string ScoreType,
        int ScoreValue,
        string PrincipalVariation,
        int? WdlWin,
        int? WdlDraw,
        int? WdlLoss)
    {
        /// <summary>
        /// Human-readable score, e.g. <c>"+1.25"</c> or <c>"Mate 3"</c>.
        /// </summary>
        public string ScoreText
        {
            get
            {
                if (ScoreType == "cp")
                {
                    return (ScoreValue / 100.0)
                        .ToString("+0.00;-0.00;0.00");
                }

                if (ScoreType == "mate")
                {
                    return $"Mate {ScoreValue}";
                }

                return $"{ScoreType} {ScoreValue}";
            }


        }

        /// <summary>
        /// The first move of <see cref="PrincipalVariation"/> in UCI notation, i.e. the move this
        /// line recommends from the analysed position. Empty if the variation is empty.
        /// </summary>
        public string RootMoveUci =>
            PrincipalVariation
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
            ?? string.Empty;

        /// <summary>
        /// Expected game score for the side to move, from 0.0 to 1.0, calculated as
        /// (win + draw / 2) / 1000. <c>null</c> if any WDL value is missing.
        /// </summary>
        public double? ExpectedPoints
        {
            get
            {
                if (WdlWin == null ||
                    WdlDraw == null ||
                    WdlLoss == null)
                {
                    return null;
                }

                return
                    (WdlWin.Value +
                     (WdlDraw.Value * 0.5))
                    / 1000.0;
            }
        }
    }
}
