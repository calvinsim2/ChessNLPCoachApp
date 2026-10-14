using ChessNLPCoachApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChessNLPCoachApp.Context
{
    // Represents the database used by ASP.NET Core Identity.
    public class ApplicationDbContext : IdentityDbContext
    {
        // Receives database settings from dependency injection.
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options) 
        {
        }

        public DbSet<DailyAnalysisUsage> DailyAnalysisUsages
            => Set<DailyAnalysisUsage>();

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            // Keep all the existing ASP.NET Identity tables.
            base.OnModelCreating(builder);

            // Configure the daily quota entity.
            builder.Entity<DailyAnalysisUsage>(entity =>
            {
                // Create one unique record per user per date.
                entity.HasKey(x => new
                {
                    x.UserId,
                    x.UtcDate
                });

                // Limit the stored user ID length.
                entity.Property(x => x.UserId)
                    .HasMaxLength(256);

                // Store the date as SQL Server 'date'.
                entity.Property(x => x.UtcDate)
                    .HasColumnType("date");
            });
        }
    }
}
