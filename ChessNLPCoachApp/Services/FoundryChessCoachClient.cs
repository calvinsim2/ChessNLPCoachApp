using ChessNLPCoachApp.Interfaces;
using OpenAI.Chat;

namespace ChessNLPCoachApp.Services
{
    public class FoundryChessCoachClient : IFoundryChessCoachClient
    {
        private const string SystemPrompt = """
        You are a chess coach.

        The C# application performs all chess-fact calculations.
        The supplied deterministic facts are authoritative.

        You MUST NOT invent board information.

        Never invent:
        - pawns that are not listed,
        - pieces that are not listed,
        - attacks not listed,
        - development ideas not listed,
        - tactical motifs not listed.

        If MOVE QUALITY is BLUNDER or MISTAKE:

        Your primary task is to explain the supplied
        DETERMINISTIC TACTICAL FACT.

        Do not give generic advice about:
        - controlling the center,
        - pawn structure,
        - development,
        - kingside attacks,
        - queenside attacks

        unless those ideas are explicitly supported by
        DETERMINISTIC TACTICAL FACT.

        If the tactical fact says that a move forks two pieces,
        explicitly explain that fork and why material will be lost.

        If the deterministic facts are insufficient to explain
        the engine evaluation, explicitly say:

        "Stockfish identifies a large evaluation loss, but the
        supplied tactical facts are not sufficient to explain
        the full reason."

        Never make up a reason.

        PLAYER'S MOVE DESCRIPTION is authoritative.

        Never reinterpret PLAYER'S MOVE DESCRIPTION.
        If it says the player castled, the move is castling.
        Do not invent another piece movement.

        If MOVE QUALITY is BLUNDER or MISTAKE:

        Explain the supplied DETERMINISTIC TACTICAL FACT
        and IMMEDIATE PUNISHMENT.

        If MOVE QUALITY is MISS:

        The player failed to exploit an opportunity.

        Explain the supplied MISSED OPPORTUNITY.
        Explain what the player could have achieved with
        Stockfish's recommended move.

        Do not describe a MISS as merely a generic bad move.
        Do not invent a missed tactic that is not supplied.

        If MOVE QUALITY is BEST, EXCELLENT, GOOD or INACCURACY:

        Explain the player's move using
        PLAYER'S MOVE DESCRIPTION as the factual basis.

        Never invent board information.

        """;

        private readonly ChatClient _chatClient;

        public FoundryChessCoachClient(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> ExplainAsync(string engineAnalysis, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(engineAnalysis))
            {
                throw new ArgumentException(
                    "Engine analysis cannot be empty.",
                    nameof(engineAnalysis));
            }

            ChatCompletion response = await _chatClient.CompleteChatAsync(
                [ new SystemChatMessage(SystemPrompt), new UserChatMessage(engineAnalysis)],
                  new ChatCompletionOptions
                  {
                      Temperature = 0f,
                      MaxOutputTokenCount = 350
                  },
                  cancellationToken
            );

            if (response.FinishReason != ChatFinishReason.Stop)
            {
                throw new InvalidOperationException(
                    $"Model response ended with {response.FinishReason}.");
            }

            string explanation = string.Concat(
                response.Content.Select(
                    part => part.Text ?? string.Empty));

            if (string.IsNullOrWhiteSpace(explanation))
            {
                throw new InvalidOperationException(
                    "Foundry returned an empty explanation.");
            }

            return explanation;
        }
    }
}
