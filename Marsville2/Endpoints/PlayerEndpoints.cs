using Marsville2.Domain;
using Marsville2.Services;

namespace Marsville2.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/players");

        // POST /api/players/register
        group.MapPost("/register", (RegisterRequest req, GameSession session, GameService gameService) =>
        {
            if (string.IsNullOrWhiteSpace(req.TeamName))
                return Results.BadRequest(new { error = "TeamName is required." });

            var round = session.CurrentRound;
            if (round is not null && round.Phase != RoundPhase.Registration)
                return Results.BadRequest(new { error = "Registration is closed. A round is already in progress." });

            var player = session.RegisterPlayer(req.TeamName);
            return Results.Ok(new { playerId = player.Id, token = player.Token, teamName = player.TeamName });
        })
        .WithName("RegisterPlayer")
        .WithOpenApi(op =>
        {
            op.Summary = "Register a new team";
            op.Description = "Call this once per agent during the registration phase. Returns a token that must be included as X-Player-Token in all subsequent requests.";
            return op;
        });

        return app;
    }

    public record RegisterRequest(string TeamName);
}
