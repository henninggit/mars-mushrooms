using Marsville2.Domain;
using Marsville2.Services;

namespace Marsville2.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");

        // POST /api/admin/rounds/create
        group.MapPost("/rounds/create", (CreateRoundRequest req, HttpContext ctx,
            GameSession session, GameService gameService, IConfiguration config) =>
        {
            if (!ValidateAdmin(ctx, session, config)) return Results.Unauthorized();

            if (req.Level is < 1)
                return Results.BadRequest(new { error = "Level must be greater than 1." });
            if (req.TimeoutSeconds <= 0)
                return Results.BadRequest(new { error = "TimeoutSeconds must be positive." });

            var round = gameService.CreateRound(req.Level, req.TimeoutSeconds, req.Seed);
            return Results.Ok(new
            {
                round.RoundId,
                round.Level,
                round.Seed,
                round.TimeoutSeconds,
                Phase = round.Phase.ToString()
            });
        })
        .WithName("AdminCreateRound")
        .WithOpenApi(op =>
        {
            op.Summary = "[Admin] Create a new round";
            op.Description = "Creates a round in Registration phase. Players can register until the admin starts the round. Requires X-Admin-Password header.";
            return op;
        });

        // POST /api/admin/rounds/start
        group.MapPost("/rounds/start", (HttpContext ctx,
            GameSession session, GameService gameService, IConfiguration config) =>
        {
            if (!ValidateAdmin(ctx, session, config)) return Results.Unauthorized();

            try
            {
                gameService.StartRound();
                return Results.Ok(new { message = "Round started.", roundId = session.CurrentRound?.RoundId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AdminStartRound")
        .WithOpenApi(op =>
        {
            op.Summary = "[Admin] Start the current round";
            op.Description = "Transitions the round from Registration to Playing. Requires X-Admin-Password header.";
            return op;
        });

        // POST /api/admin/rounds/end
        group.MapPost("/rounds/end", (HttpContext ctx,
            GameSession session, GameService gameService, IConfiguration config) =>
        {
            if (!ValidateAdmin(ctx, session, config)) return Results.Unauthorized();

            try
            {
                gameService.EndRound();
                return Results.Ok(new { message = "Round ended." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AdminEndRound")
        .WithOpenApi(op =>
        {
            op.Summary = "[Admin] Force-end the current round";
            op.Description = "Finalizes scores and ends the round immediately. Requires X-Admin-Password header.";
            return op;
        });

        // GET /api/admin/scores
        group.MapGet("/scores", (HttpContext ctx,
            GameSession session, GameService gameService, IConfiguration config) =>
        {
            if (!ValidateAdmin(ctx, session, config)) return Results.Unauthorized();
            return Results.Ok(gameService.GetLeaderboard());
        })
        .WithName("AdminGetScores")
        .WithOpenApi(op =>
        {
            op.Summary = "[Admin] Full score history";
            op.Description = "Returns all round scores and cumulative totals. Requires X-Admin-Password header.";
            return op;
        });

        return app;
    }

    private static bool ValidateAdmin(HttpContext ctx, GameSession session, IConfiguration config)
    {
        if (!ctx.Request.Headers.TryGetValue("X-Admin-Password", out var pwd))
            return false;
        return session.ValidateAdminPassword(pwd.ToString(), config);
    }

    public record CreateRoundRequest(int Level, int TimeoutSeconds, int? Seed = null);
}
