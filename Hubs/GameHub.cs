using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Qvizz.Game;

namespace Qvizz.Hubs;

public sealed class GameHub(GameService game, IConfiguration config) : Hub
{
    private const string AdminFlag = "isAdmin";

    // ---------- Admin ----------

    public async Task<bool> AdminLogin(string password)
    {
        var expected = config["Admin:Password"];
        if (string.IsNullOrEmpty(expected) || password is null) return false;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(expected));
        if (!ok) return false;

        Context.Items[AdminFlag] = true;
        await Groups.AddToGroupAsync(Context.ConnectionId, GameService.AdminsGroup);
        await game.SendAdminStateToAsync(Context.ConnectionId);
        return true;
    }

    public Task StartQuiz() => RequireAdmin() ? game.StartAsync() : Task.CompletedTask;
    public Task Next() => RequireAdmin() ? game.NextAsync() : Task.CompletedTask;
    public Task ResetGame() => RequireAdmin() ? game.NewGameAsync() : Task.CompletedTask;
    public Task NewGame() => RequireAdmin() ? game.NewGameAsync() : Task.CompletedTask;
    public Task KickPlayer(Guid playerId) => RequireAdmin() ? game.KickAsync(playerId) : Task.CompletedTask;
    public Task Pause() => RequireAdmin() ? game.PauseAsync() : Task.CompletedTask;
    public Task Resume() => RequireAdmin() ? game.ResumeAsync() : Task.CompletedTask;

    private bool RequireAdmin() =>
        Context.Items.TryGetValue(AdminFlag, out var v) && v is true
            ? true
            : throw new HubException("Not authorised.");

    // ---------- Players ----------

    public async Task<JoinResponse> Join(string nickname)
    {
        var (id, error) = await game.JoinAsync(Context.ConnectionId, nickname);
        return new JoinResponse(id, error);
    }

    public Task<bool> Rejoin(Guid playerId) => game.RejoinAsync(Context.ConnectionId, playerId);
    public Task<string?> SetAvatar(string dataUrl) => game.SetAvatarAsync(Context.ConnectionId, dataUrl);
    public Task<string?> AnswerChoice(int choice) => game.AnswerChoiceAsync(Context.ConnectionId, choice);
    public Task<string?> AnswerGrouping(int[] order) => game.AnswerGroupingAsync(Context.ConnectionId, order);
    public Task<string?> AnswerNumber(double? number) => game.AnswerNumberAsync(Context.ConnectionId, number);

    public override Task OnDisconnectedAsync(Exception? exception) => game.DisconnectedAsync(Context.ConnectionId);

    public sealed record JoinResponse(Guid? PlayerId, string? Error);
}
