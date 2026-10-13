using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Interfaces
{
    public interface IStockfishClient
    {
        Task StartAsync();
        Task<List<EngineLine>> AnalyseAsync(string fen,
                                            int depth,
                                            int multiPv,
                                            string? searchMove = null);
    }
}
