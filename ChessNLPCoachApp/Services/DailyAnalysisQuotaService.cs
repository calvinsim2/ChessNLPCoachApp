
using ChessNLPCoachApp.Configuration;
using ChessNLPCoachApp.Context;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ChessNLPCoachApp.Services
{
    public class DailyAnalysisQuotaService : IDailyAnalysisQuotaService
    {
        private readonly ApplicationDbContext _dbContext;

        public DailyAnalysisQuotaService(
            ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==============================================
        // GET REMAINING DAILY ANALYSES
        // ==============================================

        public async Task<int> GetRemainingAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            // Identify the current UTC date.
            DateOnly today =
                DateOnly.FromDateTime(DateTime.UtcNow);

            // Read the user's usage for this date.
            int? usedCount = await _dbContext.DailyAnalysisUsages
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.UtcDate == today)
                .Select(x => (int?)x.UsedCount)
                .SingleOrDefaultAsync(cancellationToken);

            // No record means the user has used zero.
            int used = usedCount ?? 0;

            return Math.Max(0, AnalysisLimits.DailyAnalysisLimit - used);
        }

        // ==============================================
        // CONSUME ONE DAILY ANALYSIS
        // ==============================================

        public async Task<bool> TryConsumeAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            DateOnly today =
                DateOnly.FromDateTime(DateTime.UtcNow);

            // First, try to increase an existing counter.
            int updatedRows = await IncrementExistingAsync(
                userId,
                today,
                cancellationToken);

            if (updatedRows == 1)
            {
                return true;
            }

            // Determine whether a counter already exists.
            bool recordExists =
                await _dbContext.DailyAnalysisUsages
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.UserId == userId &&
                        x.UtcDate == today,
                        cancellationToken);

            // An existing counter that could not be
            // incremented has reached its limit.
            if (recordExists)
            {
                return false;
            }

            // No counter exists yet for the current date.
            var newUsage = new DailyAnalysisUsage
            {
                UserId = userId,
                UtcDate = today,
                UsedCount = 1
            };

            _dbContext.DailyAnalysisUsages.Add(newUsage);

            try
            {
                // Save this user's first usage for today.
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                return true;
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx &&
                     (sqlEx.Number == 2601 ||
                      sqlEx.Number == 2627))
            {
                // Another request created the same row first.
                // Remove our failed insert from EF tracking.
                _dbContext.Entry(newUsage).State =
                    EntityState.Detached;

                // Try the existing-row increment instead.
                int retryRows = await IncrementExistingAsync(
                    userId,
                    today,
                    cancellationToken);

                return retryRows == 1;
            }
        }

        // ==============================================
        // INCREMENT AN EXISTING COUNTER ATOMICALLY
        // ==============================================

        private Task<int> IncrementExistingAsync(
            string userId,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            return _dbContext.DailyAnalysisUsages
                .Where(x =>
                    x.UserId == userId &&
                    x.UtcDate == today &&
                    x.UsedCount <
                        AnalysisLimits.DailyAnalysisLimit)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.UsedCount,
                        x => x.UsedCount + 1),
                    cancellationToken);
        }
    }
}
