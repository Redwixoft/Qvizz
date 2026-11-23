using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qvizz.Data;
using Qvizz.Hubs;
using Qvizz.Quiz;

namespace Qvizz.Game;

public sealed class GameOptions
{
    public int CountdownSeconds { get; set; } = 5;
    /// <summary>How long each question is shown on its own before the answers appear.</summary>
    public int ReadingSeconds { get; set; } = 5;
    public int QuestionSeconds { get; set; } = 30;
    public int GroupingSeconds { get; set; } = 45;
    public string QuizFile { get; set; } = "Quiz/quiz.json";
    /// <summary>Public address players open, e.g. "https://qvizz.up.railway.app/game". Shown as the lobby QR code.</summary>
    public string? PublicUrl { get; set; }
}

/// <summary>
/// The single running game.
///
/// The database is the source of truth: every change (join, drawing, answer, score, phase) is saved
/// to PostgreSQL, and on startup the active game is loaded back, so a restart or redeploy resumes
/// exactly where it was. The engine also keeps a live copy in memory so 50 phones get answers in
/// milliseconds without waiting for the database.
///
/// All state changes go through <see cref="_gate"/> so mutation and the pushes to clients happen in order.
/// </summary>
public sealed class GameService
{
    public const string AdminsGroup = "admins";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHubContext<GameHub> _hub;
    private readonly DbWriter _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly GameOptions _options;
    private readonly ILogger<GameService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly Dictionary<Guid, PlayerState> _players = [];
    private readonly Dictionary<string, Guid> _byConnection = [];

    private Guid _gameId;
    private DateTimeOffset _createdAt;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private Phase _phase = Phase.Lobby;
    private int _questionIndex = -1;
    private DateTimeOffset _phaseEndsAt;
    private TimeSpan _phaseLength;
    private int[] _groupRightOrder = [];   // display position -> pair index
    private long _answerSeq;
    private int _nextSeat = 1;
    private int _phaseVersion;              // invalidates stale timers
    private RevealInfo? _reveal;
    private IReadOnlyList<Award> _awards = [];
    private TimeSpan? _pausedRemaining;     // set while the host has paused the timer

    public QuizDefinition Quiz { get; }
    public bool Persistent => _db.Enabled;

    public GameService(IHubContext<GameHub> hub, DbWriter db, IServiceScopeFactory scopes, IOptions<GameOptions> options,
        IWebHostEnvironment env, ILogger<GameService> log)
    {
        _hub = hub;
        _db = db;
        _scopes = scopes;
        _options = options.Value;
        _log = log;
        Quiz = QuizDefinition.Load(Path.Combine(env.ContentRootPath, _options.QuizFile));
    }

    private Question CurrentQuestion => Quiz.Questions[_questionIndex];

    // ======================= Startup: load the active game =======================

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_db.Enabled) { NewGame(); return; }

            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();
            var game = await db.Games.AsNoTracking()
                .Where(g => g.Status == GameStatus.Active)
                .OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();

            if (game is null || game.QuizTitle != Quiz.DisplayName && game.Phase != nameof(Phase.Lobby))
            {
                // No game yet, or the quiz file changed under a running game: start fresh.
                if (game is not null) await ArchiveInDbAsync(db, game.Id);
                NewGame();
                return;
            }
            await RestoreAsync(db, game);
        }
        finally { _gate.Release(); }
    }

    private static async Task ArchiveInDbAsync(QvizzDb db, Guid id) =>
        await db.Games.Where(g => g.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.Status, GameStatus.Archived)
            .SetProperty(g => g.ArchivedAt, DateTimeOffset.UtcNow));

    private async Task RestoreAsync(QvizzDb db, GameEntity g)
    {
        _gameId = g.Id;
        _createdAt = g.CreatedAt;
        _startedAt = g.StartedAt;
        _finishedAt = g.FinishedAt;
        _phase = Enum.Parse<Phase>(g.Phase);
        _questionIndex = Math.Min(g.QuestionIndex, Quiz.Questions.Count - 1);
        _phaseLength = TimeSpan.FromMilliseconds(g.PhaseLengthMs);
        _phaseEndsAt = g.PhaseEndsAt ?? DateTimeOffset.UtcNow;
        _groupRightOrder = g.GroupRightOrder;
        _answerSeq = g.AnswerSeq;
        _nextSeat = g.NextSeat;
        _reveal = g.RevealJson is null ? null : JsonSerializer.Deserialize<RevealInfo>(g.RevealJson, Json);
        _pausedRemaining = g.PausedRemainingMs is int pausedMs ? TimeSpan.FromMilliseconds(pausedMs) : null;
        _awards = g.AwardsJson is null ? [] : JsonSerializer.Deserialize<List<Award>>(g.AwardsJson, Json) ?? [];

        var players = await db.Players.AsNoTracking().Where(p => p.GameId == g.Id && p.KickedAt == null).ToListAsync();
        var ids = players.Select(p => p.Id).ToList();
        var avatars = await db.Avatars.AsNoTracking().Where(a => ids.Contains(a.PlayerId)).ToDictionaryAsync(a => a.PlayerId);
        var answers = await db.Answers.AsNoTracking().Where(a => a.GameId == g.Id).ToListAsync();

        foreach (var row in players)
        {
            var p = new PlayerState
            {
                Id = row.Id, Nickname = row.Nickname, Seat = row.Seat, JoinedAt = row.JoinedAt,
                Score = row.Score, Rank = row.Rank, PrevRank = row.PrevRank, LastGain = row.LastGain, Streak = row.Streak,
                Boost = row.Boost, BoostRivalId = row.BoostRivalId, AvatarVersion = row.AvatarVersion,
                AvatarPng = avatars.TryGetValue(row.Id, out var av) ? av.Png : null
            };
            foreach (var a in answers.Where(a => a.PlayerId == row.Id))
            {
                if (a.Scored)
                {
                    p.History[a.QuestionIndex] = new AnswerRecord(a.QuestionIndex, a.AnsweredAt is not null, a.Correct,
                        a.Points, a.Bonus, a.GoodPairs, a.Changes, a.ResponseMs, a.RemainingMs, a.RankAfter ?? row.Rank,
                        a.Choice, a.Number, a.Detail ?? "", a.StreakBonus, a.StreakAfter);
                }
                if (a.QuestionIndex == _questionIndex && a.AnsweredAt is not null)
                {
                    p.CurrentAnswer = new SubmittedAnswer
                    {
                        Choice = a.Choice, Grouping = a.Grouping, Number = a.Number, At = a.AnsweredAt.Value,
                        Seq = a.Seq, Changes = a.Changes, ResponseMs = a.ResponseMs ?? 0, RemainingMs = a.RemainingMs ?? 0
                    };
                }
            }
            if (_questionIndex >= 0 && p.History.TryGetValue(_questionIndex, out var rec) && _phase != Phase.Question)
                p.LastResult = new QuestionResult(rec.Correct, rec.Points, rec.Bonus, rec.Detail, rec.StreakBonus, rec.Streak);
            _players[p.Id] = p;
        }

        _log.LogInformation("Restored game {Id} in phase {Phase} (question {Q}) with {N} players",
            _gameId, _phase, _questionIndex + 1, _players.Count);

        // Resume timers (unless the host had paused). If a deadline passed while the server was down, the transition fires right away.
        if (_pausedRemaining is not null) return;
        if (_phase == Phase.Countdown) { _phaseVersion++; ScheduleTimer(() => BeginQuestion(0)); }
        else if (_phase == Phase.Reading) { _phaseVersion++; ScheduleTimer(StartAnswering); }
        else if (_phase == Phase.Question) { _phaseVersion++; ScheduleTimer(EndQuestion); }
    }

    // ======================= Saving =======================

    private void SaveGame() => _db.Save(new GameEntity
    {
        Id = _gameId,
        QuizTitle = Quiz.DisplayName,
        Status = GameStatus.Active,
        Phase = _phase.ToString(),
        QuestionIndex = _questionIndex,
        PhaseEndsAt = _phase is Phase.Countdown or Phase.Reading or Phase.Question ? _phaseEndsAt : null,
        PhaseLengthMs = (int)_phaseLength.TotalMilliseconds,
        GroupRightOrder = _groupRightOrder,
        AnswerSeq = _answerSeq,
        NextSeat = _nextSeat,
        RevealJson = _reveal is null ? null : JsonSerializer.Serialize(_reveal, Json),
        AwardsJson = _awards.Count == 0 ? null : JsonSerializer.Serialize(_awards, Json),
        PausedRemainingMs = _pausedRemaining is { } left ? (int)left.TotalMilliseconds : null,
        CreatedAt = _createdAt,
        StartedAt = _startedAt,
        FinishedAt = _finishedAt
    });

    private void SavePlayer(PlayerState p, DateTimeOffset? kickedAt = null) => _db.Save(new PlayerEntity
    {
        Id = p.Id, GameId = _gameId, Nickname = p.Nickname, Seat = p.Seat, AvatarVersion = p.AvatarVersion,
        Score = p.Score, Rank = p.Rank, PrevRank = p.PrevRank, LastGain = p.LastGain, Streak = p.Streak,
        Boost = p.Boost, BoostRivalId = p.BoostRivalId, JoinedAt = p.JoinedAt, KickedAt = kickedAt
    });

    private void SaveAnswer(PlayerState p)
    {
        var a = p.CurrentAnswer;
        p.History.TryGetValue(_questionIndex, out var rec);
        _db.Save(new AnswerEntity
        {
            PlayerId = p.Id, QuestionIndex = _questionIndex, GameId = _gameId,
            Choice = a?.Choice, Grouping = a?.Grouping, Number = a?.Number, AnsweredAt = a?.At, Seq = a?.Seq ?? 0,
            Changes = a?.Changes ?? 0, ResponseMs = a?.ResponseMs, RemainingMs = a?.RemainingMs,
            Scored = rec is not null, Correct = rec?.Correct ?? false, Points = rec?.Points ?? 0, Bonus = rec?.Bonus ?? 0,
            GoodPairs = rec?.GoodPairs ?? 0, RankAfter = rec?.RankAfter, Detail = rec?.Detail,
            StreakBonus = rec?.StreakBonus ?? 0, StreakAfter = rec?.Streak ?? 0
        });
    }

    // ======================= Players =======================

    public async Task<(Guid? PlayerId, string? Error)> JoinAsync(string connectionId, string nickname)
    {
        nickname = (nickname ?? "").Trim();
        if (nickname.Length is 0 or > 30) return (null, "Nickname must be 1-30 characters.");

        await _gate.WaitAsync();
        try
        {
            // Nicknames are unique within the game (lobby or already running), case-insensitive.
            if (_players.Values.Any(p => string.Equals(p.Nickname, nickname, StringComparison.OrdinalIgnoreCase)))
                return (null, "That nickname is already taken. Pick another one.");
            if (_phase == Phase.Finished) return (null, "This game has finished. Wait for the next one!");

            var player = new PlayerState { Id = Guid.NewGuid(), Nickname = nickname, JoinedAt = DateTimeOffset.UtcNow, Seat = _nextSeat++ };
            _players[player.Id] = player;
            RecomputeRanks();
            player.PrevRank = player.Rank;
            SaveGame();
            SavePlayer(player);

            Attach(player, connectionId);
            await PushAdminAsync();
            await PushPlayerAsync(player);
            return (player.Id, null);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RejoinAsync(string connectionId, Guid playerId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_players.TryGetValue(playerId, out var player)) return false;
            Attach(player, connectionId);
            await PushAdminAsync();
            await PushPlayerAsync(player);
            return true;
        }
        finally { _gate.Release(); }
    }

    private void Attach(PlayerState player, string connectionId)
    {
        if (player.ConnectionId is not null) _byConnection.Remove(player.ConnectionId);
        player.ConnectionId = connectionId;
        _byConnection[connectionId] = player.Id;
    }

    public async Task DisconnectedAsync(string connectionId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_byConnection.Remove(connectionId, out var id)) return;
            if (_players.TryGetValue(id, out var p) && p.ConnectionId == connectionId) p.ConnectionId = null;
            // The last player still thinking may have just dropped out.
            if (TryEndEarly()) await PushAllAsync();
            else await PushAdminAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> SetAvatarAsync(string connectionId, string dataUrl)
    {
        const string prefix = "data:image/png;base64,";
        if (dataUrl is null || !dataUrl.StartsWith(prefix) || dataUrl.Length > 400_000) return "Invalid drawing.";
        byte[] png;
        try { png = Convert.FromBase64String(dataUrl[prefix.Length..]); }
        catch (FormatException) { return "Invalid drawing."; }

        await _gate.WaitAsync();
        try
        {
            if (!TryGetPlayer(connectionId, out var player)) return "Not joined.";
            player.AvatarPng = png;
            player.AvatarVersion++;
            SavePlayer(player);
            _db.Save(new AvatarEntity { PlayerId = player.Id, Png = png });
            await PushAdminAsync();
            await PushPlayerAsync(player);
            return null;
        }
        finally { _gate.Release(); }
    }

    public byte[]? GetAvatar(Guid playerId) =>
        _players.TryGetValue(playerId, out var p) ? p.AvatarPng : null;

    public Task<string?> AnswerChoiceAsync(string connectionId, int choice) =>
        SubmitAsync(connectionId, q =>
            q.Type is QuestionType.MultipleChoice or QuestionType.TrueFalse && choice >= 0 && choice < q.EffectiveOptions.Count
                ? new SubmittedAnswer { Choice = choice }
                : null);

    public Task<string?> AnswerGroupingAsync(string connectionId, int[] order) =>
        SubmitAsync(connectionId, q =>
            q.Type == QuestionType.Grouping && order is not null && order.Length == q.Pairs.Count &&
            order.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, q.Pairs.Count))
                ? new SubmittedAnswer { Grouping = order }
                : null);

    /// <summary>A null number clears the guess (the player emptied the input).</summary>
    public Task<string?> AnswerNumberAsync(string connectionId, double? number) =>
        number is null
            ? ClearAnswerAsync(connectionId, QuestionType.ClosestNumber)
            : SubmitAsync(connectionId, q =>
                q.Type == QuestionType.ClosestNumber && double.IsFinite(number.Value)
                    ? new SubmittedAnswer { Number = number }
                    : null);

    /// <summary>
    /// Stores (or replaces) the player's answer. Answers can be changed until the time runs out;
    /// the time of the last actual change is what counts for the speed bonus.
    /// Grouping and number answers are sent as drafts on every change, so whatever is on the
    /// phone when the time runs out is what gets scored.
    /// </summary>
    private async Task<string?> SubmitAsync(string connectionId, Func<Question, SubmittedAnswer?> build)
    {
        await _gate.WaitAsync();
        try
        {
            if (!TryGetPlayer(connectionId, out var player)) return "Not joined.";
            if (_phase != Phase.Question) return "Time is up!";

            var draft = build(CurrentQuestion);
            if (draft is null) return "Invalid answer.";
            if (SameAnswer(player.CurrentAnswer, draft)) return null;

            var now = DateTimeOffset.UtcNow;
            player.CurrentAnswer = new SubmittedAnswer
            {
                Choice = draft.Choice,
                Grouping = draft.Grouping,
                Number = draft.Number,
                At = now,
                Seq = ++_answerSeq,
                Changes = (player.CurrentAnswer?.Changes ?? 0) + 1,
                ResponseMs = (int)(now - (_phaseEndsAt - _phaseLength)).TotalMilliseconds,
                RemainingMs = Math.Max(0, (int)(_phaseEndsAt - now).TotalMilliseconds)
            };
            SaveAnswer(player);
            SaveGame();   // answer sequence

            // Multiple choice / true-false: once every connected player has an answer, don't wait for the timer.
            // Grouping / number questions always run the full time so nobody is cut off while thinking.
            if (TryEndEarly())
            {
                await PushAllAsync();
                return null;
            }

            await PushAdminAsync();
            await PushPlayerAsync(player);
            return null;
        }
        finally { _gate.Release(); }
    }

    private async Task<string?> ClearAnswerAsync(string connectionId, QuestionType type)
    {
        await _gate.WaitAsync();
        try
        {
            if (!TryGetPlayer(connectionId, out var player)) return "Not joined.";
            if (_phase != Phase.Question || CurrentQuestion.Type != type) return null;
            if (player.CurrentAnswer is null) return null;
            player.CurrentAnswer = null;
            SaveAnswer(player);
            await PushAdminAsync();
            await PushPlayerAsync(player);
            return null;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Ends a multiple-choice / true-false question once every connected player has answered.</summary>
    private bool TryEndEarly()
    {
        if (_pausedRemaining is not null) return false;   // the host paused: wait for them
        if (_phase != Phase.Question || CurrentQuestion.Type is not (QuestionType.MultipleChoice or QuestionType.TrueFalse))
            return false;
        var connected = _players.Values.Where(p => p.Connected).ToList();
        if (connected.Count == 0 || connected.Any(p => p.CurrentAnswer is null)) return false;
        EndQuestion();
        return true;
    }

    private static bool SameAnswer(SubmittedAnswer? a, SubmittedAnswer b) =>
        a is not null && a.Choice == b.Choice && a.Number == b.Number &&
        (a.Grouping ?? []).SequenceEqual(b.Grouping ?? []);

    private bool TryGetPlayer(string connectionId, out PlayerState player)
    {
        player = null!;
        return _byConnection.TryGetValue(connectionId, out var id) && _players.TryGetValue(id, out player!);
    }

    // ======================= Admin =======================

    public async Task SendAdminStateToAsync(string connectionId)
    {
        await _gate.WaitAsync();
        try { await _hub.Clients.Client(connectionId).SendAsync("adminState", BuildAdminView()); }
        finally { _gate.Release(); }
    }

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_phase != Phase.Lobby || _players.Count == 0) return;
            _startedAt = DateTimeOffset.UtcNow;
            EnterPhase(Phase.Countdown, TimeSpan.FromSeconds(_options.CountdownSeconds));
            ScheduleTimer(() => BeginQuestion(0));
            SaveGame();
            await PushAllAsync();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Freezes the countdown / question timer. Players can still change their answers.</summary>
    public async Task PauseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_phase is not (Phase.Countdown or Phase.Reading or Phase.Question) || _pausedRemaining is not null) return;
            var left = _phaseEndsAt - DateTimeOffset.UtcNow;
            _pausedRemaining = left < TimeSpan.Zero ? TimeSpan.Zero : left;
            _phaseVersion++;   // cancels the running timer
            SaveGame();
            await PushAllAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task ResumeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_pausedRemaining is not { } left) return;
            _pausedRemaining = null;
            _phaseEndsAt = DateTimeOffset.UtcNow + left;
            _phaseVersion++;
            ScheduleTimer(_phase switch { Phase.Countdown => () => BeginQuestion(0), Phase.Reading => StartAnswering, _ => EndQuestion });
            TryEndEarly();   // everyone may have answered while paused
            SaveGame();
            await PushAllAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task NextAsync()
    {
        await _gate.WaitAsync();
        try
        {
            switch (_phase)
            {
                case Phase.Reveal when _questionIndex >= Quiz.Questions.Count - 1:
                    _awards = Awards.Compute(_players.Values, Quiz);
                    if (_awards.Count > 0) EnterPhase(Phase.Awards, TimeSpan.Zero);
                    else Finish();
                    break;
                case Phase.Awards:
                    Finish();
                    break;
                case Phase.Reveal:
                    EnterPhase(Phase.Leaderboard, TimeSpan.Zero);
                    break;
                case Phase.Leaderboard:
                    BeginQuestion(_questionIndex + 1);
                    break;
                default:
                    return;
            }
            SaveGame();
            await PushAllAsync();
        }
        finally { _gate.Release(); }
    }

    private void Finish()
    {
        _finishedAt = DateTimeOffset.UtcNow;
        EnterPhase(Phase.Finished, TimeSpan.Zero);
    }

    /// <summary>Archives the current game (finished or not) and opens a fresh lobby. Phones go back to the join screen.</summary>
    public async Task NewGameAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_db.Enabled)
            {
                _db.Save(new GameEntity
                {
                    Id = _gameId, QuizTitle = Quiz.DisplayName, Status = GameStatus.Archived, Phase = _phase.ToString(),
                    QuestionIndex = _questionIndex, PhaseLengthMs = (int)_phaseLength.TotalMilliseconds,
                    GroupRightOrder = _groupRightOrder, AnswerSeq = _answerSeq, NextSeat = _nextSeat,
                    RevealJson = _reveal is null ? null : JsonSerializer.Serialize(_reveal, Json),
                    AwardsJson = _awards.Count == 0 ? null : JsonSerializer.Serialize(_awards, Json),
                    CreatedAt = _createdAt, StartedAt = _startedAt, FinishedAt = _finishedAt, ArchivedAt = DateTimeOffset.UtcNow
                });
                await _db.FlushAsync(CancellationToken.None);
            }
            NewGame();
            await _hub.Clients.All.SendAsync("reset");
            await PushAdminAsync();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Removes a player (e.g. an inappropriate nickname). They can join again under a different name.</summary>
    public async Task KickAsync(Guid playerId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_players.Remove(playerId, out var p)) return;
            if (p.ConnectionId is not null)
            {
                _byConnection.Remove(p.ConnectionId);
                await _hub.Clients.Client(p.ConnectionId).SendAsync("kicked");
            }
            SavePlayer(p, kickedAt: DateTimeOffset.UtcNow);
            RecomputeRanks();
            if (TryEndEarly()) { SaveGame(); }
            await PushAllAsync();
        }
        finally { _gate.Release(); }
    }

    // ======================= Phase machine =======================

    private void NewGame()
    {
        _phaseVersion++;
        _players.Clear();
        _byConnection.Clear();
        _phase = Phase.Lobby;
        _questionIndex = -1;
        _phaseLength = TimeSpan.Zero;
        _groupRightOrder = [];
        _answerSeq = 0;
        _nextSeat = 1;
        _reveal = null;
        _awards = [];
        _pausedRemaining = null;
        _gameId = Guid.NewGuid();
        _createdAt = DateTimeOffset.UtcNow;
        _startedAt = _finishedAt = null;
        SaveGame();
    }

    private void EnterPhase(Phase phase, TimeSpan length)
    {
        _phaseVersion++;
        _phase = phase;
        _phaseLength = length;
        _phaseEndsAt = DateTimeOffset.UtcNow + length;
        _pausedRemaining = null;
    }

    private void ScheduleTimer(Action onElapsed)
    {
        var version = _phaseVersion;
        var delay = _phaseEndsAt - DateTimeOffset.UtcNow;
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
            await _gate.WaitAsync();
            try
            {
                if (version != _phaseVersion) return; // phase already moved on
                onElapsed();
                SaveGame();
                await PushAllAsync();
            }
            catch (Exception ex) { _log.LogError(ex, "Timer transition failed"); }
            finally { _gate.Release(); }
        });
    }

    private void BeginQuestion(int index)
    {
        _questionIndex = index;
        _reveal = null;
        foreach (var p in _players.Values)
        {
            p.CurrentAnswer = null;
            p.LastResult = null;
            p.Boost = null;
            p.BoostRivalId = null;
        }

        var q = CurrentQuestion;
        if (q.Type == QuestionType.Grouping)
        {
            var n = q.Pairs.Count;
            // Shuffle so that no pair starts out correct: an untouched board scores nothing.
            do { _groupRightOrder = Enumerable.Range(0, n).OrderBy(_ => Random.Shared.Next()).ToArray(); }
            while (_groupRightOrder.Where((v, i) => v == i).Any());
        }
        else _groupRightOrder = [];

        // First the question on its own (no answers anywhere), then the answers and the answer timer.
        if (_options.ReadingSeconds > 0)
        {
            EnterPhase(Phase.Reading, TimeSpan.FromSeconds(_options.ReadingSeconds));
            ScheduleTimer(StartAnswering);
        }
        else StartAnswering();
    }

    private void StartAnswering()
    {
        var q = CurrentQuestion;
        var seconds = q.TimeLimitSeconds ?? (q.Type == QuestionType.Grouping ? _options.GroupingSeconds : _options.QuestionSeconds);
        EnterPhase(Phase.Question, TimeSpan.FromSeconds(seconds));
        ScheduleTimer(EndQuestion);
    }

    private void EndQuestion()
    {
        var q = CurrentQuestion;
        var answered = _players.Values.Where(p => p.CurrentAnswer is not null).ToList();
        var goodPairs = new Dictionary<Guid, int>();
        var keeps = new HashSet<Guid>();   // players who keep their streak on this question
        foreach (var p in _players.Values.Except(answered))
            p.LastResult = new QuestionResult(false, 0, 0, "No answer - time ran out.");

        var optionCounts = new int[q.EffectiveOptions.Count];
        var closest = new List<ClosestGuess>();
        var correctPlayers = 0;

        if (q.Type == QuestionType.ClosestNumber)
        {
            var target = q.CorrectNumber!.Value;
            var withDistance = answered.Select(p => (p, d: Math.Abs(p.CurrentAnswer!.Number!.Value - target))).ToList();
            foreach (var (p, d) in withDistance)
            {
                var rank = withDistance.Count(x => x.d < d); // ties share the better place
                var pts = Scoring.ClosestNumberPoints(rank);
                var guess = p.CurrentAnswer!.Number!.Value;
                p.LastResult = new QuestionResult(rank == 0, pts, 0,
                    d == 0 ? $"Spot on! You guessed {Fmt(guess)}." : $"You guessed {Fmt(guess)} (off by {Fmt(d)}). Place #{rank + 1}.");
                if (rank == 0) correctPlayers++;
                if (Scoring.KeepsStreakOnNumber(rank, _players.Count)) keeps.Add(p.Id);
            }
            closest = withDistance.OrderBy(x => x.d).ThenBy(x => x.p.CurrentAnswer!.Seq)
                .Take(5).Select(x => new ClosestGuess(x.p.Nickname, x.p.AvatarUrl, x.p.Seat, x.p.CurrentAnswer!.Number!.Value, x.p.LastResult!.Points)).ToList();
        }
        else
        {
            // Base points first, then speed bonus by answer order (time of the player's last change).
            // Bonus goes to correct answers; for grouping, to anyone with at least one correct pair.
            var bonusInOrder = new List<PlayerState>();
            var basePoints = new Dictionary<Guid, (int pts, string detail, bool correct)>();
            foreach (var p in answered.OrderBy(p => p.CurrentAnswer!.Seq))
            {
                var a = p.CurrentAnswer!;
                (int pts, string detail, bool correct) r;
                if (q.Type == QuestionType.Grouping)
                {
                    var good = a.Grouping!.Select((pos, left) => _groupRightOrder[pos] == left).Count(x => x);
                    goodPairs[p.Id] = good;
                    if (Scoring.KeepsStreakOnMatching(good, q.Pairs.Count)) keeps.Add(p.Id);
                    r = (good * Scoring.GroupingPointsPerPair, $"{good} of {q.Pairs.Count} pairs correct.", good == q.Pairs.Count);
                    if (good > 0) bonusInOrder.Add(p);
                }
                else
                {
                    optionCounts[a.Choice!.Value]++;
                    var ok = a.Choice == q.EffectiveCorrectIndex;
                    var full = q.Type == QuestionType.TrueFalse ? Scoring.TrueFalsePoints : Scoring.MultipleChoicePoints;
                    r = (ok ? full : 0, ok ? "Correct!" : "Not this time.", ok);
                    if (ok) { bonusInOrder.Add(p); keeps.Add(p.Id); }
                }
                basePoints[p.Id] = r;
                if (r.correct) correctPlayers++;
            }
            foreach (var p in answered)
            {
                var (pts, detail, correct) = basePoints[p.Id];
                var position = bonusInOrder.IndexOf(p);
                var bonus = position >= 0 ? Scoring.SpeedBonus(position) : 0;
                p.LastResult = new QuestionResult(correct, pts + bonus, bonus, detail);
            }
        }

        // Streaks: kept with a correct answer (matching: at least half the pairs; closest number: the better
        // half of all players). From the 3rd question in a row, each kept question earns +5.
        foreach (var p in _players.Values)
        {
            p.Streak = keeps.Contains(p.Id) ? p.Streak + 1 : 0;
            if (p.Streak >= Scoring.StreakFrom)
                p.LastResult = p.LastResult! with { Points = p.LastResult.Points + Scoring.StreakBonus, StreakBonus = Scoring.StreakBonus, Streak = p.Streak };
            else
                p.LastResult = p.LastResult! with { Streak = p.Streak };
        }

        // While nobody has scored yet everyone is tied at #1, so the first scored
        // question shows no movement arrows instead of "everyone dropped".
        var anyoneScoredBefore = _players.Values.Any(p => p.Score > 0);
        foreach (var p in _players.Values)
        {
            p.PrevRank = p.Rank;
            p.Score += p.LastResult!.Points;
            p.LastGain = p.LastResult.Points;
        }
        RecomputeRanks();
        if (!anyoneScoredBefore)
            foreach (var p in _players.Values) p.PrevRank = p.Rank;
        ComputeBoosts();

        // Keep each player's result for the awards (and for restoring after a restart).
        foreach (var p in _players.Values)
        {
            var a = p.CurrentAnswer;
            var r = p.LastResult!;
            p.History[_questionIndex] = new AnswerRecord(_questionIndex, a is not null, r.Correct, r.Points, r.Bonus,
                goodPairs.GetValueOrDefault(p.Id), a?.Changes ?? 0, a?.ResponseMs, a?.RemainingMs, p.Rank,
                a?.Choice, a?.Number, r.Detail, r.StreakBonus, r.Streak);
        }

        // The first three fully-correct players, in answer order, get their faces on the reveal.
        var firstCorrect = answered.Where(p => p.LastResult!.Correct)
            .OrderBy(p => p.CurrentAnswer!.Seq).Take(3).Select(Summary).ToList();
        var allGuesses = q.Type == QuestionType.ClosestNumber
            ? answered.Select(p => p.CurrentAnswer!.Number!.Value).ToList()
            : [];

        _reveal = new RevealInfo(
            q.EffectiveCorrectIndex, optionCounts, q.CorrectNumber, closest, allGuesses,
            q.Type == QuestionType.Grouping ? q.Pairs : [],
            firstCorrect, correctPlayers, answered.Count);

        EnterPhase(Phase.Reveal, TimeSpan.Zero);

        foreach (var p in _players.Values)
        {
            SavePlayer(p);
            SaveAnswer(p);
        }
        SaveGame();
    }

    private static string Fmt(double d) => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private List<PlayerState> Ordered() =>
        _players.Values.OrderByDescending(p => p.Score).ThenBy(p => p.Seat).ToList();

    private void RecomputeRanks()
    {
        foreach (var p in _players.Values)
            p.Rank = 1 + _players.Values.Count(o => o.Score > p.Score);
    }

    private void ComputeBoosts()
    {
        var all = _players.Values.ToList();
        foreach (var p in all)
        {
            var above = all.Where(o => o.Score > p.Score).MinBy(o => o.Score);
            var below = all.Where(o => o.Score < p.Score).MaxBy(o => o.Score);
            var tied = all.FirstOrDefault(o => o != p && o.Score == p.Score);
            var (text, rival) = all.Count == 1
                ? ("You're the only player - guaranteed gold!", null)
                : BoostMessages.For(p.Rank, p.Score, above, below, tied, final: _questionIndex >= Quiz.Questions.Count - 1);
            p.Boost = text;
            p.BoostRivalId = rival?.Id;
        }
    }

    // ======================= Views & pushes =======================

    private int RemainingMs => _pausedRemaining is { } frozen ? (int)frozen.TotalMilliseconds
        : _phase is Phase.Countdown or Phase.Reading or Phase.Question
        ? Math.Max(0, (int)(_phaseEndsAt - DateTimeOffset.UtcNow).TotalMilliseconds)
        : 0;

    private PlayerSummary Summary(PlayerState p) =>
        new(p.Id, p.Nickname, p.AvatarUrl, p.Score, p.Rank, p.PrevRank, p.LastGain,
            // speed bonus from the last scored question (shown split out on the standings)
            _questionIndex >= 0 && p.History.TryGetValue(_questionIndex, out var rec) ? rec.Bonus : 0,
            _questionIndex >= 0 && p.History.TryGetValue(_questionIndex, out var rec2) ? rec2.StreakBonus : 0,
            p.Streak,
            p.Seat, p.Connected, p.CurrentAnswer is not null);

    private PublicQuestion? BuildPublicQuestion()
    {
        if (_questionIndex < 0 || _phase is Phase.Lobby or Phase.Countdown or Phase.Awards or Phase.Finished) return null;
        var q = CurrentQuestion;
        // While the question is being read, the answers stay secret on every screen.
        if (_phase == Phase.Reading) return new PublicQuestion(q.Type, q.Text, q.Image, [], [], [], q.Unit);
        return new PublicQuestion(
            q.Type, q.Text, q.Image, q.EffectiveOptions,
            q.Pairs.Select(p => p.Left).ToList(),
            _groupRightOrder.Length == q.Pairs.Count ? _groupRightOrder.Select(i => q.Pairs[i].Right).ToList() : [],
            q.Unit);
    }

    private IReadOnlyList<AwardView> AwardViews() =>
        _awards.Where(a => _players.ContainsKey(a.WinnerId))
            .Select(a => new AwardView(a.Key, a.Title, a.Blurb, Summary(_players[a.WinnerId]), a.Stat)).ToList();

    private AdminView BuildAdminView()
    {
        var ordered = Ordered();
        var showBoard = _phase is Phase.Leaderboard or Phase.Finished;
        return new AdminView(
            _phase, Quiz.DisplayName, _questionIndex, Quiz.Questions.Count, RemainingMs, (int)_phaseLength.TotalMilliseconds,
            ordered.Select(Summary).ToList(),
            BuildPublicQuestion(),
            _players.Values.Count(p => p.CurrentAnswer is not null),
            _phase == Phase.Reveal ? _reveal : null,
            showBoard ? ordered.Take(5).Select(Summary).ToList() : [],
            _phase == Phase.Awards ? AwardViews() : [],
            string.IsNullOrWhiteSpace(_options.PublicUrl) ? null : _options.PublicUrl,
            _pausedRemaining is not null,
            _db.Enabled);
    }

    private PlayerView BuildPlayerView(PlayerState p)
    {
        string? correctAnswer = null;
        if (_phase is Phase.Reveal or Phase.Leaderboard)
        {
            var q = CurrentQuestion;
            correctAnswer = q.Type switch
            {
                QuestionType.MultipleChoice or QuestionType.TrueFalse =>
                    $"{(char)('A' + q.EffectiveCorrectIndex)}: {q.EffectiveOptions[q.EffectiveCorrectIndex]}",
                QuestionType.ClosestNumber => $"{Fmt(q.CorrectNumber!.Value)}{(q.Unit is null ? "" : " " + q.Unit)}",
                _ => null
            };
        }
        var showResult = _phase is Phase.Reveal or Phase.Leaderboard;
        var rival = p.BoostRivalId is { } rid && _players.TryGetValue(rid, out var r) ? Summary(r) : null;
        return new PlayerView(
            _phase, Summary(p), _players.Count, _questionIndex, Quiz.Questions.Count, RemainingMs, (int)_phaseLength.TotalMilliseconds,
            _phase is Phase.Reading or Phase.Question ? BuildPublicQuestion() : null,
            p.CurrentAnswer is not null,
            _phase == Phase.Question ? p.CurrentAnswer?.Choice : null,
            _phase == Phase.Question ? p.CurrentAnswer?.Grouping : null,
            _phase == Phase.Question ? p.CurrentAnswer?.Number : null,
            showResult ? p.LastResult : null,
            correctAnswer,
            showResult || _phase == Phase.Finished ? p.Boost : null,
            showResult ? rival : null,
            _phase is Phase.Awards or Phase.Finished ? AwardViews().Where(a => a.Winner.Id == p.Id).ToList() : [],
            _pausedRemaining is not null,
            _phase == Phase.Finished ? Recap(p) : []);
    }

    /// <summary>The player's answers next to the right ones, for the final phone screen.</summary>
    private IReadOnlyList<RecapItem> Recap(PlayerState p)
    {
        var items = new List<RecapItem>();
        for (var i = 0; i < Quiz.Questions.Count; i++)
        {
            var q = Quiz.Questions[i];
            p.History.TryGetValue(i, out var r);
            var answered = r?.Answered == true;
            string Opt(int idx) => $"{(char)('A' + idx)}: {q.EffectiveOptions[idx]}";
            string Num(double v) => Fmt(v) + (q.Unit is null ? "" : " " + q.Unit);
            var (yours, correct) = q.Type switch
            {
                QuestionType.MultipleChoice or QuestionType.TrueFalse =>
                    (answered && r!.Choice is int c ? Opt(c) : "No answer", Opt(q.EffectiveCorrectIndex)),
                QuestionType.ClosestNumber =>
                    (answered && r!.Number is double n ? Num(n) : "No answer", Num(q.CorrectNumber!.Value)),
                _ => (answered ? $"{r!.GoodPairs} of {q.Pairs.Count} pairs right" : "No answer", (string?)null)
            };
            items.Add(new RecapItem(i + 1, q.Text, yours, correct, r?.Points ?? 0, r?.Correct ?? false, answered));
        }
        return items;
    }

    private Task PushAdminAsync() => _hub.Clients.Group(AdminsGroup).SendAsync("adminState", BuildAdminView());

    private Task PushPlayerAsync(PlayerState p) =>
        p.ConnectionId is null ? Task.CompletedTask : _hub.Clients.Client(p.ConnectionId).SendAsync("playerState", BuildPlayerView(p));

    private async Task PushAllAsync()
    {
        await PushAdminAsync();
        foreach (var p in _players.Values) await PushPlayerAsync(p);
    }
}
