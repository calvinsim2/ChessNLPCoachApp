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
    }
}
