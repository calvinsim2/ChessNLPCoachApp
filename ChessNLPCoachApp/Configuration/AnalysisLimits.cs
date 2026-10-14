namespace ChessNLPCoachApp.Configuration
{
    public static class AnalysisLimits
    {
        // Maximum PGN file size: 64 KB.
        public const long MaxPgnFileBytes = 64 * 1024;

        // Maximum half-moves per game.
        public const int MaxPlyCount = 80;

        // Maximum accepted analyses per user, per UTC day.
        public const int DailyAnalysisLimit = 3;

        // Additional short-window protection:
        // Maximum upload attempts per hour, per user.
        public const int HourlyUploadAttempts = 10;
    }
}
