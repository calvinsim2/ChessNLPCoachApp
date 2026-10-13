using ChessNLPCoachApp.Records;

namespace ChessNLPCoachApp.Models
{
    public class GameAnalysisViewModel
    {
        public string FileName { get; set; } =
            string.Empty;

        public List<MoveAnalysis> MoveAnalyses { get; set; } =
            new List<MoveAnalysis>();
    }
}
