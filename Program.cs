using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Qvizz.Data;
using Qvizz.Game;
using Qvizz.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Railway (and similar hosts) tell the app which port to listen on.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));

builder.Services.AddDbContext<QvizzDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Qvizz")));
builder.Services.AddSingleton<DbWriter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DbWriter>());
builder.Services.AddSingleton<GameService>();

builder.Services
    .AddSignalR(o => o.MaximumReceiveMessageSize = 512 * 1024) // avatar drawings
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Bring the database schema up to date, then load the active game (a restart resumes it).
var writer = app.Services.GetRequiredService<DbWriter>();
if (writer.Enabled)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<QvizzDb>().Database.MigrateAsync();
}
else
{
    app.Logger.LogWarning("ConnectionStrings:Qvizz is not set - the game runs in memory only and is lost on restart.");
}
await app.Services.GetRequiredService<GameService>().InitializeAsync();

app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/game"));
app.MapGet("/admin", (IWebHostEnvironment env) => Results.File(Path.Combine(env.WebRootPath, "admin.html"), "text/html"));
app.MapGet("/game", (IWebHostEnvironment env) => Results.File(Path.Combine(env.WebRootPath, "game.html"), "text/html"));
app.MapGet("/host", (IWebHostEnvironment env) => Results.File(Path.Combine(env.WebRootPath, "host.html"), "text/html"));
app.MapGet("/avatar/{id:guid}", (Guid id, GameService game) =>
    game.GetAvatar(id) is { } png ? Results.File(png, "image/png") : Results.NotFound());

app.MapHub<GameHub>("/hub");

app.Run();
