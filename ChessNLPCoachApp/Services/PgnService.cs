using Chess;
using ChessNLPCoachApp.Helpers;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Records;
using System.Text;
using System.Text.RegularExpressions;

namespace ChessNLPCoachApp.Services
{
    public class PgnService : IPgnService
    {
        public async Task<List<PgnMoveContext>> GetMoveContextsAsync(
        string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"PGN file was not found: {filePath}");
            }

            string pgnContent =
                await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(pgnContent))
            {
                throw new InvalidDataException(
                    "The PGN file is empty.");
            }

            // First let Gera.Chess validate that the PGN
            // represents a valid chess game.
            bool success = ChessBoard.TryLoadFromPgn(
                pgnContent,
                out ChessBoard? loadedBoard);

            if (!success || loadedBoard is null)
            {
                throw new InvalidDataException(
                    "The PGN file could not be parsed.");
            }

            // Extract the main-line SAN moves.
            List<string> sanMoves =
                ExtractMainLineMoves(pgnContent);

            // Start a fresh chess board.
            // We will now replay the PGN ourselves one move at a time.
            var board = new ChessBoard();

            var contexts =
                new List<PgnMoveContext>();

            for (int i = 0; i < sanMoves.Count; i++)
            {
                string san = sanMoves[i];

                int ply = i + 1;

                int moveNumber =
                    (i / 2) + 1;

                string side =
                    i % 2 == 0
                        ? "White"
                        : "Black";


                // ==========================================
                // POSITION BEFORE PLAYER'S MOVE
                // ==========================================

                string fenBeforeMove =
                    board.ToFen();


                // ==========================================
                // PLAY THE MOVE FROM THE PGN
                // ==========================================

                board.Move(san);


                // ==========================================
                // POSITION AFTER PLAYER'S MOVE
                // ==========================================

                string fenAfterMove =
                    board.ToFen();


                string uci = ConvertToUci(fenBeforeMove,
                                          fenAfterMove,
                                          san,
                                          side);


                contexts.Add(
                    new PgnMoveContext(
                        Ply: ply,
                        MoveNumber: moveNumber,
                        Side: side,
                        San: san,
                        Uci: uci,
                        FenBeforeMove: fenBeforeMove,
                        FenAfterMove: fenAfterMove
                    )
                );
            }

            return contexts;
        }


        private static List<string> ExtractMainLineMoves(
            string pgn)
        {
            // Remove PGN headers:
            //
            // [Event "..."]
            // [White "..."]
            // etc.
            string movesText = Regex.Replace(
                pgn,
                @"^\s*\[.*?\]\s*$",
                string.Empty,
                RegexOptions.Multiline);


            // Remove {...} comments.
            movesText = Regex.Replace(
                movesText,
                @"\{.*?\}",
                string.Empty,
                RegexOptions.Singleline);


            // Remove ; comments.
            movesText = Regex.Replace(
                movesText,
                @";.*$",
                string.Empty,
                RegexOptions.Multiline);


            // Remove PGN variations:
            //
            // (3... Nf6 4. O-O)
            movesText =
                RemoveVariations(movesText);


            // Remove Numeric Annotation Glyphs:
            //
            // $1
            // $2
            // etc.
            movesText = Regex.Replace(
                movesText,
                @"\$\d+",
                string.Empty);


            // Remove move numbers:
            //
            // 1.
            // 1...
            // 2.
            movesText = Regex.Replace(
                movesText,
                @"\b\d+\.(?:\.\.)?",
                " ");


            string[] tokens =
                movesText.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries);


            var moves =
                new List<string>();

            foreach (string rawToken in tokens)
            {
                string token =
                    rawToken.Trim();

                // Ignore game result.
                if (token is
                    "1-0" or
                    "0-1" or
                    "1/2-1/2" or
                    "*")
                {
                    continue;
                }

                // Remove annotation symbols:
                //
                // e4!
                // Nf3?!
                token = Regex.Replace(
                    token,
                    @"[!?]+$",
                    string.Empty);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    moves.Add(token);
                }
            }

            return moves;
        }


        private static string RemoveVariations(
            string text)
        {
            var builder =
                new StringBuilder();

            int depth = 0;

            foreach (char c in text)
            {
                if (c == '(')
                {
                    depth++;
                    continue;
                }

                if (c == ')')
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    continue;
                }

                if (depth == 0)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static string ConvertToUci(string fenBeforeMove,
                                           string fenAfterMove,
                                           string san,
                                           string side)
        {
            // ==============================================
            // 1. HANDLE CASTLING
            // ==============================================

            string normalizedSan =
                san.Replace("0", "O");

            if (normalizedSan.StartsWith("O-O-O"))
            {
                return side == "White"
                    ? "e1c1"
                    : "e8c8";
            }

            if (normalizedSan.StartsWith("O-O"))
            {
                return side == "White"
                    ? "e1g1"
                    : "e8g8";
            }


            // ==============================================
            // 2. READ BOTH CHESS BOARDS
            // ==============================================

            Dictionary<string, char> beforeBoard =
                ChessBoardAnalysisHelper.ParseFenBoard(fenBeforeMove);

            Dictionary<string, char> afterBoard =
                ChessBoardAnalysisHelper.ParseFenBoard(fenAfterMove);


            // ==============================================
            // 3. DETERMINE WHICH COLOUR MOVED
            // ==============================================

            bool isWhite =
                side == "White";


            // ==============================================
            // 4. FIND THE MOVING PIECE'S STARTING SQUARE
            // ==============================================

            string? fromSquare = null;

            foreach (KeyValuePair<string, char> square
                     in beforeBoard)
            {
                char piece =
                    square.Value;

                bool belongsToMovingSide =
                    isWhite
                        ? char.IsUpper(piece)
                        : char.IsLower(piece);

                if (!belongsToMovingSide)
                {
                    continue;
                }

                bool samePieceStillThere =
                    afterBoard.TryGetValue(
                        square.Key,
                        out char afterPiece)
                    &&
                    afterPiece == piece;

                if (!samePieceStillThere)
                {
                    fromSquare =
                        square.Key;

                    break;
                }
            }


            // ==============================================
            // 5. FIND DESTINATION SQUARE FROM SAN
            // ==============================================

            Match destinationMatch =
                Regex.Match(
                    san,
                    @"([a-h][1-8])(?:=[QRBN])?[+#]?$");

            if (!destinationMatch.Success)
            {
                throw new InvalidDataException(
                    $"Could not determine destination square from SAN move '{san}'.");
            }

            string toSquare =
                destinationMatch.Groups[1].Value;


            // ==============================================
            // 6. MAKE SURE STARTING SQUARE WAS FOUND
            // ==============================================

            if (string.IsNullOrWhiteSpace(fromSquare))
            {
                throw new InvalidDataException(
                    $"Could not determine starting square for SAN move '{san}'.");
            }


            // ==============================================
            // 7. HANDLE PAWN PROMOTION
            // ==============================================

            string promotion = string.Empty;

            Match promotionMatch =
                Regex.Match(
                    san,
                    @"=([QRBN])");

            if (promotionMatch.Success)
            {
                promotion =
                    promotionMatch.Groups[1]
                        .Value
                        .ToLowerInvariant();
            }


            // ==============================================
            // 8. BUILD UCI MOVE
            // ==============================================

            return
                fromSquare +
                toSquare +
                promotion;
        }
    }
}
