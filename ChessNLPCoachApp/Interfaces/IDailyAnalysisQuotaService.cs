namespace ChessNLPCoachApp.Interfaces
{
    /// <summary>
    /// Tracks how many analyses a user has run on the current UTC day and enforces
    /// <see cref="Configuration.AnalysisLimits.DailyAnalysisLimit"/>.
    /// </summary>
    public interface IDailyAnalysisQuotaService
    {
        /// <summary>
        /// Gets how many analyses the user has left today. Never negative.
        /// </summary>
        Task<int> GetRemainingAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically uses one of the user's analyses for today.
        /// </summary>
        /// <returns><c>true</c> if one was used; <c>false</c> if the daily limit was already reached.</returns>
        Task<bool> TryConsumeAsync(string userId, CancellationToken cancellationToken = default);
    }
}
