namespace ChessNLPCoachApp.Records
{
    /// <summary>
    /// Everything about one move from a PGN game, including the board position
    /// before and after it.
    /// </summary>
    /// <remarks>
    /// Both FEN strings are needed so Stockfish can analyse the position before and after the move.
    /// Example FEN (starting position):
    /// <c>rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1</c>
    /// </remarks>
    /// <param name="Ply">
    /// Half-move index, starting at 1. Odd = a move by White, even = a move by Black.
    /// </param>
    /// <param name="MoveNumber">
    /// Full-move number as written in notation. In <c>1. d4 d5</c>, both moves are move 1.
    /// </param>
    /// <param name="Side">The player who made the move: <c>"White"</c> or <c>"Black"</c>.</param>
    /// <param name="San">The move in Standard Algebraic Notation, e.g. <c>Nf3</c> or <c>Rxf5</c>.</param>
    /// <param name="Uci">
    /// The move in UCI notation (from-square to to-square), e.g. <c>g1f3</c>.
    /// This is the format Stockfish uses.
    /// </param>
    /// <param name="FenBeforeMove">FEN of the position before the move was played.</param>
    /// <param name="FenAfterMove">FEN of the position after the move was played.</param>
    public sealed record PgnMoveContext(
        int Ply,
        int MoveNumber,
        string Side,
        string San,
        string Uci,
        string FenBeforeMove,
        string FenAfterMove);
}
