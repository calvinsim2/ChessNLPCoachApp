namespace ChessNLPCoachApp.Models
{
    public class DailyAnalysisUsage
    {
        // The ID of the authenticated Identity user.
        public string UserId { get; set; } = string.Empty;

        // The UTC calendar date for this quota.
        public DateOnly UtcDate { get; set; }

        // Number of analyses accepted on this date.
        public int UsedCount { get; set; }
    }
}
