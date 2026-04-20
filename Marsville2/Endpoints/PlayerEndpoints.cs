using Marsville2.Domain;
using Marsville2.Services;
using Microsoft.Extensions.Configuration;

namespace Marsville2.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/players");

        // POST /api/players/register
        group.MapPost("/register", (RegisterRequest req, GameSession session, GameService gameService, IConfiguration config, HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(req.TeamName))
                return Results.BadRequest(new { error = "TeamName is required." });

            // Optional registration gate: if RegistrationSecret is configured in settings,
            // callers must supply the matching X-Registration-Key header.
            var requiredSecret = config["RegistrationSecret"];
            if (!string.IsNullOrEmpty(requiredSecret))
            {
                var providedKey = http.Request.Headers["X-Registration-Key"].FirstOrDefault();
                if (providedKey != requiredSecret)
                    return Results.Unauthorized();
            }

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
            op.Description = "Call this once per agent during the registration phase. Returns a token that must be included as X-Player-Token in all subsequent requests. " +
                             "If the server is configured with a RegistrationSecret, you must also supply a matching X-Registration-Key header.";
            return op;
        });

        return app;
    }

    public record RegisterRequest(string TeamName);
}
