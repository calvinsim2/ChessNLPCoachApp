using ChessNLPCoachApp.Enums;
using ChessNLPCoachApp.Records;
using System.Text;

namespace ChessNLPCoachApp.Helpers
{
    /// <summary>
    /// Builds the text sent to Langflow so the language model can explain a move.
    /// </summary>
    /// <remarks>
    /// The prompt has three variants: a missed opportunity, a mistake or blunder, and every
    /// other move. Each one tells the model which supplied facts to rely on, so it doesn't
    /// invent board facts or tactics.
    /// </remarks>
    public static class CoachingPromptBuilder
    {
        /// <summary>
        /// Builds the coaching prompt for one played move.
        /// </summary>
        /// <param name="move">The move as parsed from the PGN.</param>
        /// <param name="score">The move's losses and quality category, which also choose the prompt variant.</param>
        /// <param name="facts">The deterministic board facts the model must base its explanation on.</param>
        /// <returns>The prompt text, one labelled fact per line followed by a coaching task.</returns>
        public static string Build(PgnMoveContext move,
                                   MoveScore score,
                                   MoveFacts facts)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                $"Player: {move.Side}");

            builder.AppendLine(
                $"Move Number: {move.MoveNumber}");

            builder.AppendLine(
                $"Player's Move: {move.San}");

            builder.AppendLine(
                $"PLAYER'S MOVE DESCRIPTION: " +
                $"{facts.PlayedMoveDescription}");

            builder.AppendLine();

            builder.AppendLine(
                $"MOVE QUALITY: {score.Quality}");

            builder.AppendLine(
                $"CENTIPAWN LOSS: {score.CentipawnLoss}");

            builder.AppendLine(
                $"EXPECTED POINT LOSS: " +
                $"{score.ExpectedPointLoss:0.000}");

            builder.AppendLine();

            builder.AppendLine(
                $"STOCKFISH RANK 1 MOVE: " +
                $"{facts.BestMoveDescription}");

            builder.AppendLine();

            if (score.Quality == MoveQuality.Miss)
            {
                builder.AppendLine(
                    "MISSED OPPORTUNITY:");

                builder.AppendLine(
                    facts.BestMoveTacticalFact);

                builder.AppendLine();

                builder.AppendLine(
                    "COACHING TASK:");

                builder.AppendLine(
                    "Explain what winning opportunity " +
                    "the player failed to exploit.");

                builder.AppendLine(
                    "Use the MISSED OPPORTUNITY fact.");

                builder.AppendLine(
                    "Do not describe the move as a blunder " +
                    "unless the supplied facts justify it.");

                builder.AppendLine(
                    "Do not invent tactical motifs.");

                return builder.ToString();
            }

            if (score.Quality is
                MoveQuality.Mistake or
                MoveQuality.Blunder)
            {
                builder.AppendLine(
                    "IMMEDIATE PUNISHMENT:");

                builder.AppendLine(
                    facts.OpponentBestReplyDescription);

                builder.AppendLine();

                builder.AppendLine(
                    "DETERMINISTIC TACTICAL FACT:");

                builder.AppendLine(
                    facts.TacticalConsequence);

                builder.AppendLine();

                builder.AppendLine(
                    "COACHING TASK:");

                builder.AppendLine(
                    $"The player's move is a {score.Quality}.");

                builder.AppendLine(
                    "Explain specifically why the move is bad.");

                builder.AppendLine(
                    "Use the deterministic tactical fact.");

                builder.AppendLine(
                    "Focus on the concrete punishment.");

                builder.AppendLine(
                    "Do not invent strategic explanations.");

                return builder.ToString();
            }

            builder.AppendLine(
                "COACHING TASK:");

            builder.AppendLine(
                "Explain the main chess idea behind " +
                "the player's move.");

            builder.AppendLine(
                "Use PLAYER'S MOVE DESCRIPTION as " +
                "the authoritative description of the move.");

            builder.AppendLine(
                "Do not invent board facts.");

            return builder.ToString();
        }
    }
}
