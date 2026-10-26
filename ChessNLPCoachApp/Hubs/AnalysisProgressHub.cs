using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChessNLPCoachApp.Hubs
{
    [Authorize]
    public class AnalysisProgressHub : Hub
    {
        // Construct a unique group for one user's analysis.
        public static string GroupName(string userId, Guid analysisId)
        {
            return $"analysis:{userId}:{analysisId:N}";
        }

        // Called by JavaScript before uploading the PGN.
        public async Task JoinAnalysis(string analysisId)
        {
            // Do not accept an invalid analysis identifier.
            if (!Guid.TryParse(analysisId, out Guid id))
            {
                throw new HubException("Invalid analysis identifier.");
            }

            // Identify the authenticated user.
            string userId = Context.UserIdentifier ?? throw new HubException("Authentication is required.");

            // Subscribe this connection to its analysis group.
            await Groups.AddToGroupAsync(Context.ConnectionId,
                                         GroupName(userId, id));
        }
    }
}
