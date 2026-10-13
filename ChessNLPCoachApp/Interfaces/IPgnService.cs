using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Interfaces
{
    public interface IPgnService
    {
        Task<List<PgnMoveContext>> GetMoveContextsAsync(string filePath);
    }
}
