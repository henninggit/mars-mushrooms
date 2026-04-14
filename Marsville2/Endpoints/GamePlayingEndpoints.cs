using Marsville2.Domain;
using Marsville2.Services;

namespace Marsville2.Endpoints;

public static class GamePlayingEndpoints
{
    public static IEndpointRouteBuilder MapGamePlayingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/game");

        // GET /api/game/state  — returns the player's current visible board state
        group.MapGet("/state", (HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();

            var state = gameService.GetState(player);
            if (state is null)
                return Results.BadRequest(new { error = "No active round or you have not joined yet." });

            return Results.Ok(state);
        })
        .WithName("GetState")
        .WithOpenApi(op =>
        {
            op.Summary = "Get the current visible board state";
            op.Description = "Returns cells within your vision radius, your position, health, backpack contents, and mushroom count.";
            return op;
        });

        // POST /api/game/move
        group.MapPost("/move", (DirectionRequest req, HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "move", req.Direction);
            return MapResult(result, state);
        })
        .WithName("Move")
        .WithOpenApi(op =>
        {
            op.Summary = "Move in a direction";
            op.Description = "Direction: 0=East, 1=West, 2=North, 3=South";
            return op;
        });

        // POST /api/game/jump
        group.MapPost("/jump", (DirectionRequest req, HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "jump", req.Direction);
            return MapResult(result, state);
        })
        .WithName("Jump")
        .WithOpenApi(op =>
        {
            op.Summary = "Jump over a single hole cell";
            op.Description = "Moves the player 2 cells in the given direction over a HoleCell. Direction: 0=East, 1=West, 2=North, 3=South";
            return op;
        });

        // POST /api/game/crawl
        group.MapPost("/crawl", (DirectionRequest req, HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "crawl", req.Direction);
            return MapResult(result, state);
        })
        .WithName("Crawl")
        .WithOpenApi(op =>
        {
            op.Summary = "Crawl through a low obstacle";
            op.Description = "Enters a LowObstacleCell. Direction: 0=East, 1=West, 2=North, 3=South";
            return op;
        });

        // POST /api/game/pickup
        group.MapPost("/pickup", (HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "pickup");
            return MapResult(result, state);
        })
        .WithName("Pickup")
        .WithOpenApi(op =>
        {
            op.Summary = "Pick up an item from the current cell";
            op.Description = "Stores the item in your spacesuit pockets (backpack). Mushrooms are collected automatically when you step on them.";
            return op;
        });

        // POST /api/game/build
        group.MapPost("/build", (DirectionRequest req, HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "build", req.Direction);
            return MapResult(result, state);
        })
        .WithName("Build")
        .WithOpenApi(op =>
        {
            op.Summary = "Repair an adjacent broken bridge";
            op.Description = "Consumes 1 Plank + 1 Nail from spacesuit pockets. The adjacent cell must be a BrokenBridgeCell. Direction: 0=East, 1=West, 2=North, 3=South";
            return op;
        });

        // POST /api/game/attack
        group.MapPost("/attack", (DirectionRequest req, HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "attack", req.Direction);
            return MapResult(result, state);
        })
        .WithName("Attack")
        .WithOpenApi(op =>
        {
            op.Summary = "Attack an adjacent enemy";
            op.Description = "Deals 1 damage to the entity 1 cell away in the given direction. Direction: 0=East, 1=West, 2=North, 3=South";
            return op;
        });

        // POST /api/game/wait
        group.MapPost("/wait", (HttpContext ctx, GameSession session, GameService gameService) =>
        {
            var player = ResolvePlayer(ctx, session);
            if (player is null) return Results.Unauthorized();
            var (result, state) = gameService.PerformAction(player, "wait");
            return MapResult(result, state);
        })
        .WithName("Wait")
        .WithOpenApi(op =>
        {
            op.Summary = "Skip your turn";
            op.Description = "Enemies still move after a wait action.";
            return op;
        });

        // GET /api/game/rounds
        group.MapGet("/rounds", (GameService gameService) =>
            Results.Ok(gameService.GetLeaderboard()))
        .WithName("GetRounds")
        .WithOpenApi(op =>
        {
            op.Summary = "Leaderboard and round history";
            op.Description = "Returns cumulative team scores and the history of all completed rounds.";
            return op;
        });

        return app;
    }

    private static Domain.Entities.Player? ResolvePlayer(HttpContext ctx, GameSession session)
    {
        if (!ctx.Request.Headers.TryGetValue("X-Player-Token", out var token))
            return null;
        return session.GetPlayerByToken(token.ToString());
    }

    private static IResult MapResult(ActionResult result, object? state)
    {
        return result switch
        {
            ActionResult.Ok or ActionResult.GoalReached =>
                Results.Ok(new { result = result.ToString(), state }),
            ActionResult.NotPlaying =>
                Results.BadRequest(new { error = "No active round or registration still open." }),
            ActionResult.PlayerDead =>
                Results.BadRequest(new { error = "Your astronaut has been eliminated." }),
            ActionResult.CellBlocked =>
                Results.BadRequest(new { error = "That cell is blocked." }),
            ActionResult.CannotJump =>
                Results.BadRequest(new { error = "Cannot jump in that direction — no hole, or landing cell is blocked." }),
            ActionResult.CannotCrawl =>
                Results.BadRequest(new { error = "Cannot crawl in that direction." }),
            ActionResult.NoItemToPickUp =>
                Results.BadRequest(new { error = "No item to pick up here." }),
            ActionResult.BackpackFull =>
                Results.BadRequest(new { error = "Spacesuit pockets are full (max 10 items)." }),
            ActionResult.MissingBridgeMaterials =>
                Results.BadRequest(new { error = "Need at least 1 plank and 1 nail in your spacesuit pockets." }),
            ActionResult.NoBrokenBridgeAdjacent =>
                Results.BadRequest(new { error = "No broken bridge in that direction." }),
            ActionResult.NothingToAttack =>
                Results.BadRequest(new { error = "Nothing to attack in that direction." }),
            _ =>
                Results.BadRequest(new { error = result.ToString() })
        };
    }

    public record DirectionRequest(int Direction);
}