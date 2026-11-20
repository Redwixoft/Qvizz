using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace Qvizz.Data;

/// <summary>One quiz session. Exactly one session is "Active"; finished or reset ones are archived.</summary>
public sealed class GameEntity
{
    public Guid Id { get; set; }
    public string QuizTitle { get; set; } = "";
    public string Status { get; set; } = GameStatus.Active;
    public string Phase { get; set; } = "Lobby";
    public int QuestionIndex { get; set; } = -1;
    public DateTimeOffset? PhaseEndsAt { get; set; }
    public int PhaseLengthMs { get; set; }
    /// <summary>Matching questions: display position -> pair index for the current question.</summary>
    public int[] GroupRightOrder { get; set; } = [];
    public long AnswerSeq { get; set; }
    public int NextSeat { get; set; } = 1;
    /// <summary>The current reveal (votes, closest guesses…) as JSON, so a restart shows the same screen.</summary>
    public string? RevealJson { get; set; }
    public string? AwardsJson { get; set; }
    /// <summary>When the host paused the timer: how much time was left. Null when running.</summary>
    public int? PausedRemainingMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
}

public static class GameStatus
{
    public const string Active = "Active";
    public const string Archived = "Archived";
}

public sealed class PlayerEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string Nickname { get; set; } = "";
    public int Seat { get; set; }
    public int AvatarVersion { get; set; }
    public int Score { get; set; }
    public int Rank { get; set; }
    public int PrevRank { get; set; }
    public int LastGain { get; set; }
    public int Streak { get; set; }
    public string? Boost { get; set; }
    public Guid? BoostRivalId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? KickedAt { get; set; }
}

/// <summary>The drawn face, kept apart so score updates don't rewrite the image.</summary>
public sealed class AvatarEntity
{
    public Guid PlayerId { get; set; }
    public byte[] Png { get; set; } = [];
}

/// <summary>
/// One row per player per question. Updated live while the player changes their answer,
/// then finalised with points when the question ends (also for players who didn't answer).
/// </summary>
public sealed class AnswerEntity
{
    public Guid PlayerId { get; set; }
    public int QuestionIndex { get; set; }
    public Guid GameId { get; set; }
    public int? Choice { get; set; }
    public int[]? Grouping { get; set; }
    public double? Number { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public long Seq { get; set; }
    /// <summary>How many times the answer was set or changed.</summary>
    public int Changes { get; set; }
    public int? ResponseMs { get; set; }
    public int? RemainingMs { get; set; }
    public bool Scored { get; set; }
    public bool Correct { get; set; }
    public int Points { get; set; }
    public int Bonus { get; set; }
    public int GoodPairs { get; set; }
    public int StreakBonus { get; set; }
    public int StreakAfter { get; set; }
    public int? RankAfter { get; set; }
    public string? Detail { get; set; }
}

public sealed class QvizzDb(DbContextOptions<QvizzDb> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<AvatarEntity> Avatars => Set<AvatarEntity>();
    public DbSet<AnswerEntity> Answers => Set<AnswerEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<GameEntity>(e =>
        {
            e.ToTable("games");
            e.Property(g => g.Status).HasMaxLength(20);
            e.Property(g => g.Phase).HasMaxLength(20);
            e.Property(g => g.RevealJson).HasColumnType("jsonb");
            e.Property(g => g.AwardsJson).HasColumnType("jsonb");
            e.HasIndex(g => g.Status);
        });
        b.Entity<PlayerEntity>(e =>
        {
            e.ToTable("players");
            e.Property(p => p.Nickname).HasMaxLength(40);
            e.HasIndex(p => p.GameId);
            e.HasOne<GameEntity>().WithMany().HasForeignKey(p => p.GameId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AvatarEntity>(e =>
        {
            e.ToTable("avatars");
            e.HasKey(a => a.PlayerId);
            e.HasOne<PlayerEntity>().WithOne().HasForeignKey<AvatarEntity>(a => a.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AnswerEntity>(e =>
        {
            e.ToTable("answers");
            e.HasKey(a => new { a.PlayerId, a.QuestionIndex });
            e.HasIndex(a => a.GameId);
            e.HasOne<PlayerEntity>().WithMany().HasForeignKey(a => a.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>
/// Saves game state to PostgreSQL. The game engine keeps a live copy in memory (it has to answer
/// 50 phones within milliseconds); every change is handed here as a snapshot of the changed row.
/// Snapshots are coalesced by key, so a burst of changes becomes one small batch, written in order.
/// On startup the engine reloads the active game from the database, so a restart or redeploy resumes the game.
/// </summary>
public sealed class DbWriter(IServiceScopeFactory scopes, ILogger<DbWriter> log, IConfiguration config) : BackgroundService
{
    private readonly ConcurrentDictionary<object, object> _pending = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public bool Enabled { get; } = !string.IsNullOrWhiteSpace(config.GetConnectionString("Qvizz"));

    public void Save(GameEntity g) => Queue(("game", g.Id), g);
    public void Save(PlayerEntity p) => Queue(("player", p.Id), p);
    public void Save(AvatarEntity a) => Queue(("avatar", a.PlayerId), a);
    public void Save(AnswerEntity a) => Queue(("answer", a.PlayerId, a.QuestionIndex), a);

    private void Queue(object key, object row)
    {
        if (!Enabled) return;
        _pending[key] = row;          // latest snapshot of this row wins
        if (_signal.CurrentCount == 0) _signal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await _signal.WaitAsync(ct);
                await Task.Delay(60, ct);   // let a burst of answers collect into one batch
                await FlushAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        if (Enabled) await FlushAsync(ct);   // don't lose the last moves on a redeploy
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        await _flushLock.WaitAsync(ct);
        try
        {
            while (!_pending.IsEmpty)
            {
                var batch = new List<object>();
                foreach (var key in _pending.Keys.ToList())
                    if (_pending.TryRemove(key, out var row)) batch.Add(row);
                try { await WriteAsync(batch, ct); }
                catch (Exception ex) { log.LogError(ex, "Saving game state failed ({Count} rows)", batch.Count); }
            }
        }
        finally { _flushLock.Release(); }
    }

    private async Task WriteAsync(List<object> batch, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();

        // Parents before children so foreign keys are satisfied.
        var games = batch.OfType<GameEntity>().ToList();
        var players = batch.OfType<PlayerEntity>().ToList();
        var avatars = batch.OfType<AvatarEntity>().ToList();
        var answers = batch.OfType<AnswerEntity>().ToList();

        await UpsertAsync(db, db.Games, games, g => g.Id, ids => db.Games.Where(g => ids.Contains(g.Id)), ct);
        await UpsertAsync(db, db.Players, players, p => p.Id, ids => db.Players.Where(p => ids.Contains(p.Id)), ct);
        await UpsertAsync(db, db.Avatars, avatars, a => a.PlayerId, ids => db.Avatars.Where(a => ids.Contains(a.PlayerId)), ct);

        if (answers.Count > 0)
        {
            var pids = answers.Select(a => a.PlayerId).Distinct().ToList();
            var qis = answers.Select(a => a.QuestionIndex).Distinct().ToList();
            var existing = await db.Answers.Where(a => pids.Contains(a.PlayerId) && qis.Contains(a.QuestionIndex)).ToListAsync(ct);
            foreach (var row in answers)
            {
                var e = existing.FirstOrDefault(x => x.PlayerId == row.PlayerId && x.QuestionIndex == row.QuestionIndex);
                if (e is null) db.Answers.Add(row); else db.Entry(e).CurrentValues.SetValues(row);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task UpsertAsync<T>(QvizzDb db, DbSet<T> set, List<T> rows, Func<T, Guid> key,
        Func<List<Guid>, IQueryable<T>> load, CancellationToken ct) where T : class
    {
        if (rows.Count == 0) return;
        var existing = await load(rows.Select(key).ToList()).ToDictionaryAsync(key, ct);
        foreach (var row in rows)
        {
            if (existing.TryGetValue(key(row), out var e)) db.Entry(e).CurrentValues.SetValues(row);
            else set.Add(row);
        }
        await db.SaveChangesAsync(ct);
    }
}
