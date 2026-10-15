namespace ChessNLPCoachApp.Interfaces
{
    /// <summary>
    /// Gets move explanations from a local Langflow flow. Intended for development and prompt
    /// testing.
    /// </summary>
    public interface ILangflowClient
    {
        Task<string> AskLangFlowAsync(string engineAnalysis);
    }
}
