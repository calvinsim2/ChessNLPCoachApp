using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Helpers
{
    public static class ChessBoardAnalysisHelper
    {
        public static string GetOpponentBestReplyUci(
            EngineLine playedMoveLine)
        {
            string[] moves =
                playedMoveLine.PrincipalVariation.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            if (moves.Length < 2)
            {
                return string.Empty;
            }

            return moves[1];
        }


        public static string DescribeUciMove(
            string fenBeforeMove,
            string uci)
        {
            if (string.IsNullOrWhiteSpace(uci) || uci.Length < 4)
            {
                return "No move information is available.";
            }

            Dictionary<string, char> board = ParseFenBoard(fenBeforeMove);

            // A B C D E F G
            // 0 1 2 3 4 5 6
            // 
            // [..2]     → A B  - basically take from start till index 2 - 1 = 1
            // [2..]     → C D E F G - basically takes from index 2 all the way till end.
            // [2..5]    → C D E - takes from index 2 till index 5 - 1 = 4.
            string fromSquare = uci[..2];

            string toSquare = uci.Substring(2, 2);

            if (!board.TryGetValue(fromSquare, out char movingPiece))
            {
                return $"UCI move {uci}";
            }

            string movingSide = char.IsUpper(movingPiece) ? "White" : "Black";

            string? castlingDescription = DescribeCastlingMove(movingPiece,
                                                               fromSquare,
                                                               toSquare);

            if (castlingDescription != null)
            {
                return castlingDescription;
            }

            string movingPieceName = GetPieceName(movingPiece);

            string description = $"{movingSide} {movingPieceName} " +
                                 $"moves from {fromSquare} to {toSquare}";

            if (board.TryGetValue(toSquare, out char capturedPiece))
            {
                string capturedSide = char.IsUpper(capturedPiece) ? "White" : "Black";

                string capturedPieceName = GetPieceName(capturedPiece);

                description += $", capturing the {capturedSide} " + $"{capturedPieceName} on {toSquare}";
            }

            return description + ".";
        }

        private static string? DescribeCastlingMove(char movingPiece,
                                                    string fromSquare,
                                                    string toSquare)
        {
            if (char.ToLowerInvariant(movingPiece) != 'k')
            {
                return null;
            }

            return (fromSquare, toSquare) switch
            {
                ("e1", "g1") =>
                    "White castles kingside. " +
                    "The White king moves from e1 to g1, " +
                    "and the White rook moves from h1 to f1.",

                ("e1", "c1") =>
                    "White castles queenside. " +
                    "The White king moves from e1 to c1, " +
                    "and the White rook moves from a1 to d1.",

                ("e8", "g8") =>
                    "Black castles kingside. " +
                    "The Black king moves from e8 to g8, " +
                    "and the Black rook moves from h8 to f8.",

                ("e8", "c8") =>
                    "Black castles queenside. " +
                    "The Black king moves from e8 to c8, " +
                    "and the Black rook moves from a8 to d8.",

                _ => null
            };
        }
        public static string DescribeImmediateTacticalConsequence(string fenBeforeReply,
                                                                  string replyUci)
        {
            if (string.IsNullOrWhiteSpace(replyUci) || replyUci.Length < 4)
            {
                return
                    "No immediate tactical reply was available.";
            }

            Dictionary<string, char> board = ParseFenBoard(fenBeforeReply);

            string fromSquare = replyUci[..2];

            string toSquare = replyUci.Substring(2, 2);

            if (!board.TryGetValue(fromSquare, out char movingPiece))
            {
                return
                    $"The opponent's best reply is {replyUci}.";
            }

            string moveDescription = DescribeUciMove(fenBeforeReply,
                                                     replyUci);

            Dictionary<string, char> boardAfterReply = new Dictionary<string, char>(board);

            boardAfterReply.Remove(fromSquare);

            char resultingPiece = movingPiece;

            // UCI promotions look like:
            // e7e8q
            if (replyUci.Length >= 5)
            {
                char promotionPiece =
                    replyUci[4];

                resultingPiece = char.IsUpper(movingPiece) ? char.ToUpperInvariant(promotionPiece) : 
                                                             char.ToLowerInvariant(promotionPiece);
            }

            boardAfterReply[toSquare] = resultingPiece;

            List<string> attackedSquares = GetAttackedSquares(toSquare,
                                                              resultingPiece,
                                                              boardAfterReply);

            bool movingPieceIsWhite = char.IsUpper(resultingPiece);

            List<string> attackedEnemyPieces = new List<string>();

            foreach (string attackedSquare in attackedSquares)
            {
                if (!boardAfterReply.TryGetValue(attackedSquare, out char targetPiece))
                {
                    continue;
                }

                bool targetIsWhite = char.IsUpper(targetPiece);

                if (targetIsWhite == movingPieceIsWhite)
                {
                    continue;
                }

                string targetSide = targetIsWhite ? "White" : "Black";

                string targetPieceName = GetPieceName(targetPiece);

                attackedEnemyPieces.Add($"{targetSide} {targetPieceName} " + $"on {attackedSquare}");
            }

            if (attackedEnemyPieces.Count >= 2)
            {
                return
                    $"{moveDescription} " + $"It simultaneously attacks " +
                    $"{string.Join(" and ", attackedEnemyPieces)}. " + $"This creates a fork.";
            }

            if (attackedEnemyPieces.Count == 1)
            {
                return
                    $"{moveDescription} " + $"It attacks " + $"{attackedEnemyPieces[0]}.";
            }

            return moveDescription;
        }


        private static List<string> GetAttackedSquares(string square,
                                                       char piece,
                                                       Dictionary<string, char> board)
        {
            List<string> attackedSquares = new List<string>();

            int file = square[0] - 'a';

            int rank = square[1] - '1';

            char pieceType = char.ToLowerInvariant(piece);

            if (pieceType == 'n')
            {
                int[,] knightOffsets =
                {
                    { 1, 2 },
                    { 2, 1 },
                    { 2, -1 },
                    { 1, -2 },
                    { -1, -2 },
                    { -2, -1 },
                    { -2, 1 },
                    { -1, 2 }
                };

                for (int i = 0; i < knightOffsets.GetLength(0); i++)
                {
                    AddSquareIfValid(attackedSquares,
                                     file + knightOffsets[i, 0],
                                     rank + knightOffsets[i, 1]);
                }

                return attackedSquares;
            }


            if (pieceType == 'p')
            {
                int direction = char.IsUpper(piece) ? 1 : -1;

                AddSquareIfValid(attackedSquares,
                                 file - 1,
                                 rank + direction);


                AddSquareIfValid(attackedSquares,
                                 file + 1,
                                 rank + direction);


                return attackedSquares;
            }


            if (pieceType == 'k')
            {
                for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
                {
                    for (int rankOffset = -1; rankOffset <= 1; rankOffset++)
                    {
                        if (fileOffset == 0 && rankOffset == 0)
                        {
                            continue;
                        }

                        AddSquareIfValid(attackedSquares,
                                         file + fileOffset,
                                         rank + rankOffset);
                    }
                }

                return attackedSquares;
            }


            if (pieceType is 'b' or 'q')
            {
                AddSlidingAttacks(attackedSquares,
                                  board,
                                  file,
                                  rank,
                                  1,
                                  1);


                AddSlidingAttacks(attackedSquares,
                                  board,
                                  file,
                                  rank,
                                  1,
                                  -1);

                AddSlidingAttacks(attackedSquares,
                                  board,
                                  file,
                                  rank,
                                  -1,
                                  1);

                AddSlidingAttacks(attackedSquares,
                                  board,
                                  file,
                                  rank,
                                  -1,
                                  -1);
            }


            if (pieceType is 'r' or 'q')
            {
                AddSlidingAttacks(
                    attackedSquares,
                    board,
                    file,
                    rank,
                    1,
                    0);

                AddSlidingAttacks(
                    attackedSquares,
                    board,
                    file,
                    rank,
                    -1,
                    0);

                AddSlidingAttacks(
                    attackedSquares,
                    board,
                    file,
                    rank,
                    0,
                    1);

                AddSlidingAttacks(
                    attackedSquares,
                    board,
                    file,
                    rank,
                    0,
                    -1);
            }

            return attackedSquares;
        }


        private static void AddSlidingAttacks(List<string> attackedSquares,
                                              Dictionary<string, char> board,
                                              int startFile,
                                              int startRank,
                                              int fileDirection,
                                              int rankDirection)
        {
            int file = startFile + fileDirection;

            int rank = startRank + rankDirection;

            while (IsValidSquare(file, rank))
            {
                string square = ToSquare(file, rank);

                attackedSquares.Add(square);

                if (board.ContainsKey(square))
                {
                    break;
                }

                file += fileDirection;

                rank += rankDirection;
            }
        }


        private static void AddSquareIfValid(List<string> squares,
                                             int file,
                                             int rank)
        {
            if (!IsValidSquare(file, rank))
            {
                return;
            }

            squares.Add(ToSquare(file,rank));
        }


        private static bool IsValidSquare(int file,
                                          int rank)
        {
            return file >= 0 && 
                   file <= 7 && 
                   rank >= 0 && 
                   rank <= 7;
        }


        private static string ToSquare(int file, int rank)
        {
            char fileCharacter = (char)('a' + file);

            char rankCharacter = (char)('1' + rank);

            return $"{fileCharacter}{rankCharacter}";
        }


        private static string GetPieceName(char piece)
        {
            return char.ToLowerInvariant(piece) switch
            {
                'p' => "pawn",
                'n' => "knight",
                'b' => "bishop",
                'r' => "rook",
                'q' => "queen",
                'k' => "king",
                _ => "piece"
            };
        }


        public static Dictionary<string, char> ParseFenBoard(string fen)
        {
            Dictionary<string, char> board = new Dictionary<string, char>();

            // ** Remember, FEN uses UPPERCASE for WHITE pieces, and lowercase for BLACK pieces.
            // Consider a typical fen, etc: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1
            // this line helps us takes only the chessboard portion, which is this: rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR
            string boardPart = fen.Split(' ')[0];

            // by doing this line, we get each rank of the chess board:
            // ranks[0] = rnbqkbnr    ← rank 8
            // ranks[1] = pppppppp    ← rank 7
            // ranks[2] = 8           ← rank 6
            // ranks[3] = 8           ← rank 5
            // ranks[4] = 8           ← rank 4
            // ranks[5] = 8           ← rank 3
            // ranks[6] = PPPPPPPP    ← rank 2
            // ranks[7] = RNBQKBNR    ← rank 1

            string[] ranks = boardPart.Split('/');

            for (int rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
            {
                string rank = ranks[rankIndex];

                int fileIndex = 0;

                foreach (char symbol in rank)
                {
                    // this line handle FEN numbers, for example:
                    // 8 - means 8 empty squares
                    // 3P4 - means 3 empty squares | White pawn | 4 empty squares
                    if (char.IsDigit(symbol))
                    {
                        // C# characters internally have numeric Unicode values. etc: '0' = 48, '1' = 49, '2' = 50, '3' = 51, ....
                        // thus, considering our "3P4" example, during the first iteration of this rank foreach loop
                        // symbol is '3', thus symbol - '0' is equivalent to: '3' - '0', and internally it becomes 51 - 48.
                        // 51 - 48 gives us 3, thus in this case, we increment the fileIndex by 3. This make sense as we skip the empty files
                        // of a, b and c on the chessboard as there is nothing on it, then we 'continue', because we dont want to add empty squares
                        // into our dictionary.
                        fileIndex += symbol - '0';

                        continue;
                    }

                    // this line basically converts:
                    // 0 → a
                    // 1 → b
                    // 2 → c
                    // 3 → d
                    // 4 → e
                    // 5 → f
                    // 6 → g
                    // 7 → h

                    // again, referring to "3P4" example. When the iteration is at 'P', since 'P' is not a digit, we will enter this part of the code
                    // at this iteration, our 'fileIndex' is currently at 3.
                    // Remember, characters have numeric representations as well. For example: 'a' = 97, 'b' = 98, 'c' = 99, 'd' = 100, ....
                    // thus, 'a' + 3 becomes 97 + 3 = 100.
                    // but when we do 'a' + 3, it returns 100, which is an int. but what we want is a char
                    // thus we do this (char) conversion. Now, it gies us 'd'
                    char file = (char)('a' + fileIndex);

                    int rankNumber = 8 - rankIndex;

                    // this produce strings such as: e2, d1, h8.
                    string square = $"{file}{rankNumber}";

                    // this produce stuff such as: "a1" → 'R', "b8" → 'n'
                    board[square] = symbol;

                    // if this file iteration we are at indeed has a piece sitting on it, we need to increment the file by 1 so we can move and see
                    // the next file along the same rank.
                    fileIndex++;
                }
            }

            // Eventually, we have a dictionary like this: { "a1" : "R", "a2", "N", ..... }
            return board;
        }
    }
}
