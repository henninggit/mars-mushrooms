using Marsville2.Domain;
using Marsville2.Endpoints;
using Marsville2.Hubs;
using Marsville2.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ Services
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// CORS — allow the React UI (and any additional origins from config)
var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// Domain + game services (singletons so state is shared across requests)
builder.Services.AddSingleton<GameSession>();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

// ------------------------------------------------------------------ Middleware
app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// ------------------------------------------------------------------ Endpoints
app.MapPlayerEndpoints();
app.MapGamePlayingEndpoints();
app.MapAdminEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();
