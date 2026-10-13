namespace ChessNLPCoachApp.Interfaces
{
    public interface ILangflowClient
    {
        Task<string> AskLangFlowAsync(string engineAnalysis);
    }
}
