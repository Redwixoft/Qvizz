using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qvizz.Data;
using Qvizz.Quiz;

namespace Qvizz.Game;

// ---------- DTOs for the presenter's "Old games" screens ----------

public sealed record HistoryGameSummary(
    Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    int Players, int QuestionsPlayed, int QuestionCount, string? Winner, int WinnerScore, bool Current);

/// <summary>One line of a matching / ordering answer ("Anna → Ukraine", "2. Milan"), right or wrong; Image when the answer is a picture.</summary>
public sealed record HistoryPart(string Text, bool Ok, string? Image = null, string? LeftImage = null);

public sealed record HistoryQuestion(
    int Number, QuestionType Type, string Text, string? Image, string? Correct, IReadOnlyList<HistoryPart> CorrectParts,
    string? Explanation = null);

/// <summary>Result: "good" (fully right), "part" (some points), "bad" (no points) or "none" (no answer).</summary>
public sealed record HistoryAnswer(
    int Question, string Result, int Points, int Bonus, int StreakBonus, string Short, string Text,
    IReadOnlyList<HistoryPart> Parts, int? ResponseMs, int Changes);

public sealed record HistoryPlayer(
    Guid Id, string Nickname, string? AvatarUrl, int Seat, int Score, int Rank, int CorrectCount, IReadOnlyList<HistoryAnswer> Answers);

public sealed record HistoryAward(string Title, string Blurb, string Stat, Guid WinnerId);

/// <summary>
/// Everything about one game. QuestionsFromCurrentQuiz: the game was saved before games kept their own copy of
/// the questions, so the current quiz.json is shown instead (it may have changed since).
/// </summary>
public sealed record HistoryGame(
    HistoryGameSummary Game, IReadOnlyList<HistoryQuestion> Questions, IReadOnlyList<HistoryPlayer> Players,
    IReadOnlyList<HistoryAward> Awards, bool QuestionsFromCurrentQuiz);

/// <summary>A game's raw results, from memory (this game, or old ones without a database) or from the database.</summary>
internal sealed record ArchivedPlayer(Guid Id, string Nickname, int Seat, int Score, int Rank, int AvatarVersion, bool HasAvatar,
    IReadOnlyDictionary<int, AnswerRecord> History);

internal sealed record ArchivedGame(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    IReadOnlyList<Question>? Questions, IReadOnlyList<ArchivedPlayer> Players, IReadOnlyList<Award> Awards,
    IReadOnlyDictionary<Guid, byte[]> Avatars);

public sealed partial class GameService
{
    private readonly string _quizJson;
    private readonly List<ArchivedGame> _memoryArchive = [];   // finished games when there is no database (lost on restart)
    private const int MemoryArchiveSize = 20;

    private ArchivedGame SnapshotCurrent() => new(
        _gameId, Quiz.DisplayName, _createdAt, _startedAt, _finishedAt, Quiz.Questions,
        _players.Values.Select(p => new ArchivedPlayer(p.Id, p.Nickname, p.Seat, p.Score, p.Rank, p.AvatarVersion,
            p.AvatarPng is not null, new Dictionary<int, AnswerRecord>(p.History))).ToList(),
        _awards,
        _players.Values.Where(p => p.AvatarPng is not null).ToDictionary(p => p.Id, p => p.AvatarPng!));

    private void KeepInMemory(ArchivedGame g)
    {
        lock (_memoryArchive)
        {
            _memoryArchive.Insert(0, g);
            if (_memoryArchive.Count > MemoryArchiveSize) _memoryArchive.RemoveAt(_memoryArchive.Count - 1);
        }
    }

    private byte[]? ArchivedAvatar(Guid playerId)
    {
        lock (_memoryArchive)
            foreach (var g in _memoryArchive)
                if (g.Avatars.TryGetValue(playerId, out var png)) return png;
        return null;
    }

    /// <summary>A face from this game, an old game kept in memory, or the database.</summary>
    public async Task<byte[]?> GetAvatarAsync(Guid playerId)
    {
        if (GetAvatar(playerId) is { } png) return png;
        if (!_db.Enabled) return null;
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();
        return await db.Avatars.AsNoTracking().Where(a => a.PlayerId == playerId).Select(a => a.Png).FirstOrDefaultAsync();
    }

    // ======================= Listing =======================

    /// <summary>Played games, newest first: this game (once started) and up to 100 older ones.</summary>
    public async Task<IReadOnlyList<HistoryGameSummary>> ListGamesAsync()
    {
        ArchivedGame current;
        await _gate.WaitAsync();
        try { current = SnapshotCurrent(); }
        finally { _gate.Release(); }

        var list = new List<HistoryGameSummary>();
        if (current.StartedAt is not null) list.Add(Summarize(current, true));

        if (!_db.Enabled)
        {
            lock (_memoryArchive) list.AddRange(_memoryArchive.Select(g => Summarize(g, false)));
            return list;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();
        var games = await db.Games.AsNoTracking()
            .Where(g => g.StartedAt != null && g.Id != current.Id)
            .OrderByDescending(g => g.CreatedAt).Take(100).ToListAsync();
        var ids = games.Select(g => g.Id).ToList();
        var players = await db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.GameId) && p.KickedAt == null)
            .Select(p => new { p.GameId, p.Nickname, p.Score }).ToListAsync();
        var played = await db.Answers.AsNoTracking()
            .Where(a => ids.Contains(a.GameId) && a.Scored)
            .GroupBy(a => a.GameId).Select(x => new { x.Key, Last = x.Max(a => a.QuestionIndex) })
            .ToDictionaryAsync(x => x.Key, x => x.Last + 1);

        foreach (var g in games)
        {
            var ps = players.Where(p => p.GameId == g.Id).ToList();
            var top = ps.Count == 0 ? 0 : ps.Max(p => p.Score);
            var winners = ps.Where(p => p.Score == top).Select(p => p.Nickname).ToList();
            list.Add(new HistoryGameSummary(g.Id, g.QuizTitle, g.CreatedAt, g.StartedAt, g.FinishedAt, ps.Count,
                played.GetValueOrDefault(g.Id), ReadQuestions(g.QuizJson)?.Count ?? 0,
                winners.Count == 0 ? null : string.Join(", ", winners), top, false));
        }
        return list;
    }

    private static HistoryGameSummary Summarize(ArchivedGame g, bool current)
    {
        var top = g.Players.Count == 0 ? 0 : g.Players.Max(p => p.Score);
        var winners = g.Players.Where(p => p.Score == top).OrderBy(p => p.Seat).Select(p => p.Nickname).ToList();
        return new HistoryGameSummary(g.Id, g.Title, g.CreatedAt, g.StartedAt, g.FinishedAt, g.Players.Count,
            QuestionsPlayed(g), g.Questions?.Count ?? 0, winners.Count == 0 ? null : string.Join(", ", winners), top, current);
    }

    private static int QuestionsPlayed(ArchivedGame g) =>
        g.Players.SelectMany(p => p.History.Keys).DefaultIfEmpty(-1).Max() + 1;

    private static List<Question>? ReadQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<Question>>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Permanently removes an old game (e.g. a test run) with its players, faces and answers.
    /// The game being played now can't be removed. Returns an error message, or null when it's gone.
    /// </summary>
    public async Task<string?> DeleteGameAsync(Guid id)
    {
        await _gate.WaitAsync();
        try { if (id == _gameId) return "This is the game being played now. Start a new game first."; }
        finally { _gate.Release(); }

        lock (_memoryArchive)
            if (_memoryArchive.RemoveAll(g => g.Id == id) > 0) return null;
        if (!_db.Enabled) return "That game could not be found.";

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();
        // players, their faces and answers go with the game (cascading foreign keys)
        var removed = await db.Games.Where(g => g.Id == id && g.Status == GameStatus.Archived).ExecuteDeleteAsync();
        if (removed == 0) return "That game could not be found.";
        _log.LogInformation("Deleted old game {Id}", id);
        return null;
    }

    // ======================= One game =======================

    public async Task<HistoryGame?> GetGameAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            if (id == _gameId) return Build(SnapshotCurrent(), true, false);
        }
        finally { _gate.Release(); }

        lock (_memoryArchive)
            if (_memoryArchive.FirstOrDefault(g => g.Id == id) is { } kept) return Build(kept, false, false);

        if (!_db.Enabled) return null;
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QvizzDb>();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return null;

        var rows = await db.Players.AsNoTracking().Where(p => p.GameId == id && p.KickedAt == null).ToListAsync();
        var ids = rows.Select(p => p.Id).ToList();
        var withFace = (await db.Avatars.AsNoTracking().Where(a => ids.Contains(a.PlayerId)).Select(a => a.PlayerId).ToListAsync()).ToHashSet();
        var answers = await db.Answers.AsNoTracking().Where(a => a.GameId == id && a.Scored).ToListAsync();

        var players = rows.Select(r => new ArchivedPlayer(r.Id, r.Nickname, r.Seat, r.Score, r.Rank, r.AvatarVersion, withFace.Contains(r.Id),
            answers.Where(a => a.PlayerId == r.Id).ToDictionary(a => a.QuestionIndex, a => new AnswerRecord(
                a.QuestionIndex, a.AnsweredAt is not null, a.Correct, a.Points, a.Bonus, a.GoodPairs, a.Changes, a.ResponseMs,
                a.RemainingMs, a.RankAfter ?? r.Rank, a.Choice, a.Number, a.Detail ?? "", a.StreakBonus, a.StreakAfter, a.Arranged))))
            .ToList();

        var questions = ReadQuestions(game.QuizJson);
        // Saved before games kept their questions: the current quiz is the best guess, if it is the same quiz.
        var fromCurrent = questions is null && game.QuizTitle == Quiz.DisplayName;
        var awards = string.IsNullOrEmpty(game.AwardsJson) ? [] : JsonSerializer.Deserialize<List<Award>>(game.AwardsJson, Json) ?? [];
        var archived = new ArchivedGame(game.Id, game.QuizTitle, game.CreatedAt, game.StartedAt, game.FinishedAt,
            questions ?? (fromCurrent ? Quiz.Questions : null), players, awards, new Dictionary<Guid, byte[]>());
        return Build(archived, false, fromCurrent);
    }

    private static HistoryGame Build(ArchivedGame g, bool current, bool fromCurrent)
    {
        var played = QuestionsPlayed(g);
        var count = Math.Max(played, g.Questions?.Count ?? 0);
        Question? Q(int i) => g.Questions is { } qs && i < qs.Count ? qs[i] : null;

        var questions = Enumerable.Range(0, count).Select(i =>
        {
            var q = Q(i);
            if (q is null) return new HistoryQuestion(i + 1, QuestionType.MultipleChoice, $"Question {i + 1}", null, null, []);
            var (correct, parts) = CorrectAnswer(q);
            return new HistoryQuestion(i + 1, q.Type, q.Text, q.Image, correct, parts,
                string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim());
        }).ToList();

        var players = g.Players.OrderBy(p => p.Rank).ThenByDescending(p => p.Score).ThenBy(p => p.Seat).Select(p =>
        {
            var answers = Enumerable.Range(0, played).Select(i => Describe(i, Q(i), p.History.GetValueOrDefault(i))).ToList();
            return new HistoryPlayer(p.Id, p.Nickname, p.HasAvatar ? $"/avatar/{p.Id}?v={p.AvatarVersion}" : null, p.Seat,
                p.Score, p.Rank, answers.Count(a => a.Result == "good"), answers);
        }).ToList();

        var ids = g.Players.Select(p => p.Id).ToHashSet();
        var awards = g.Awards.Where(a => ids.Contains(a.WinnerId)).Select(a => new HistoryAward(a.Title, a.Blurb, a.Stat, a.WinnerId)).ToList();
        return new HistoryGame(Summarize(g, current), questions, players, awards, fromCurrent);
    }

    // ----- turning answers into words -----

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string WithUnit(Question q, double v) => Num(v) + (string.IsNullOrWhiteSpace(q.Unit) ? "" : " " + q.Unit);
    private static string ItemText(QuizItem it) =>
        !string.IsNullOrWhiteSpace(it.Text) ? it.Text.Trim()
        : Path.GetFileNameWithoutExtension(it.Image ?? "") is { Length: > 0 } file ? file : "picture";
    /// <summary>"prefix text", or the prefix with the picture when the item is only a picture (a flag, say).</summary>
    private static HistoryPart Piece(string prefix, QuizItem it, bool ok) =>
        !string.IsNullOrWhiteSpace(it.Text) || string.IsNullOrWhiteSpace(it.Image)
            ? new HistoryPart($"{prefix} {ItemText(it)}", ok) : new HistoryPart(prefix, ok, it.Image);
    /// <summary>"Left → right"; a left side that is only a picture (a flag) is shown as that picture.</summary>
    private static HistoryPart Pair(QuizItem left, QuizItem right, bool ok) =>
        string.IsNullOrWhiteSpace(left.Text) && !string.IsNullOrWhiteSpace(left.Image)
            ? Piece("→", right, ok) with { LeftImage = left.Image }
            : Piece($"{ItemText(left)} →", right, ok);
    private static string Option(Question q, int i) =>
        i >= 0 && i < q.EffectiveOptions.Count ? $"{(char)('A' + i)}: {q.EffectiveOptions[i]}" : "?";

    private static (string? Text, IReadOnlyList<HistoryPart> Parts) CorrectAnswer(Question q) => q.Type switch
    {
        QuestionType.MultipleChoice or QuestionType.TrueFalse => (Option(q, q.EffectiveCorrectIndex), []),
        QuestionType.ClosestNumber => (q.CorrectNumber is { } n ? WithUnit(q, n) : null, []),
        QuestionType.Grouping => (null, q.Pairs.Select(p => Pair(p.Left, p.Right, true)).ToList()),
        QuestionType.Ordering => (null, q.Items.Select((it, i) => Piece($"{i + 1}.", it, true)).ToList()),
        _ => (null, [])
    };

    private static HistoryAnswer Describe(int index, Question? q, AnswerRecord? r)
    {
        if (r is null || !r.Answered)
            return new HistoryAnswer(index, "none", r?.Points ?? 0, 0, r?.StreakBonus ?? 0, "–", "No answer", [], null, 0);

        var result = r.Correct ? "good" : r.Points - r.Bonus - r.StreakBonus > 0 ? "part" : "bad";
        string shortText, text;
        IReadOnlyList<HistoryPart> parts = [];
        switch (q?.Type)
        {
            case QuestionType.MultipleChoice or QuestionType.TrueFalse:
                shortText = r.Choice is int c ? ((char)('A' + c)).ToString() : "?";
                text = r.Choice is int c2 ? Option(q, c2) : "?";
                break;
            case QuestionType.ClosestNumber:
                shortText = r.Number is double n ? Num(n) : "?";
                text = r.Number is double n2 && q.CorrectNumber is double target
                    ? WithUnit(q, n2) + (n2 == target ? " (spot on)" : $" ({Num(Math.Abs(n2 - target))} {(n2 > target ? "over" : "under")})")
                    : r.Detail;
                break;
            case QuestionType.Grouping or QuestionType.Ordering:
                shortText = $"{r.GoodPairs}/{q.Pieces}";
                text = q.Type == QuestionType.Grouping ? $"{r.GoodPairs} of {q.Pieces} pairs right" : $"{r.GoodPairs} of {q.Pieces} in the right place";
                if (r.Arranged is { } arr && arr.Length == q.Pieces && arr.All(x => x >= 0 && x < q.Pieces))
                    parts = q.Type == QuestionType.Grouping
                        ? arr.Select((pair, row) => Pair(q.Pairs[row].Left, q.Pairs[pair].Right, pair == row)).ToList()
                        : arr.Select((item, place) => Piece($"{place + 1}.", q.Items[item], item == place)).ToList();
                break;
            default:   // question unknown (old game, quiz changed): fall back to what was saved
                shortText = r.Choice is int c3 ? ((char)('A' + c3)).ToString() : r.Number is double n3 ? Num(n3) : r.GoodPairs > 0 ? r.GoodPairs.ToString() : "?";
                text = r.Detail;
                break;
        }
        return new HistoryAnswer(index, result, r.Points, r.Bonus, r.StreakBonus, shortText, text, parts, r.ResponseMs, r.Changes);
    }
}
