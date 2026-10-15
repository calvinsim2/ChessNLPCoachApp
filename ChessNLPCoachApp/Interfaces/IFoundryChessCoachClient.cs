namespace ChessNLPCoachApp.Interfaces
{
    /// <summary>
    /// Gets move explanations from the hosted Azure AI Foundry model. This is the production
    /// provider.
    /// </summary>
    public interface IFoundryChessCoachClient
    {
        Task<string> ExplainAsync(string engineAnalysis);
    }
}
