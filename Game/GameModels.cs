using Qvizz.Quiz;

namespace Qvizz.Game;

public enum Phase
{
    Lobby,
    Countdown,
    Question,
    Reveal,
    Leaderboard,
    Awards,
    Finished,
    /// <summary>First part of each question: only the question (and picture) are shown, answers come after.</summary>
    Reading
}

/// <summary>A player's current (still changeable) answer to the running question.</summary>
public sealed class SubmittedAnswer
{
    public int? Choice { get; init; }
    public int[]? Grouping { get; init; }
    public double? Number { get; init; }
    public DateTimeOffset At { get; init; }
    /// <summary>Global submission order, used to rank the speed bonus.</summary>
    public long Seq { get; init; }
    /// <summary>How many times the answer was set or changed during this question.</summary>
    public int Changes { get; init; }
    public int ResponseMs { get; init; }
    public int RemainingMs { get; init; }
}

/// <summary>Points = answer + speed bonus + streak bonus. Streak = questions in a row the player has "kept" (see Scoring).</summary>
public sealed record QuestionResult(bool Correct, int Points, int Bonus, string Detail, int StreakBonus = 0, int Streak = 0);

/// <summary>A scored question for one player; kept for the awards and for restoring after a restart.</summary>
public sealed record AnswerRecord(
    int QuestionIndex,
    bool Answered,
    bool Correct,
    int Points,
    int Bonus,
    int GoodPairs,
    int Changes,
    int? ResponseMs,
    int? RemainingMs,
    int RankAfter,
    int? Choice,
    double? Number,
    string Detail,
    int StreakBonus = 0,
    int Streak = 0,
    /// <summary>Matching / ordering: the player's arrangement in quiz order (pair or item index per row / place).</summary>
    int[]? Arranged = null);

public sealed class PlayerState
{
    public Guid Id { get; init; }
    public string Nickname { get; set; } = "";
    public byte[]? AvatarPng { get; set; }
    public int AvatarVersion { get; set; }
    public int Score { get; set; }
    public int Rank { get; set; }
    /// <summary>Rank before the last scored question, for the up/down arrows.</summary>
    public int PrevRank { get; set; }
    /// <summary>Points from the last scored question (kept while the next question runs, for the ticker).</summary>
    public int LastGain { get; set; }
    /// <summary>Questions in a row the player has kept (correct, or good enough on matching / closest-number).</summary>
    public int Streak { get; set; }
    /// <summary>1-based join order; also picks the player's colour.</summary>
    public int Seat { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
    public string? ConnectionId { get; set; }

    public SubmittedAnswer? CurrentAnswer { get; set; }
    public QuestionResult? LastResult { get; set; }
    public string? Boost { get; set; }
    public Guid? BoostRivalId { get; set; }
    public Dictionary<int, AnswerRecord> History { get; } = [];

    public bool Connected => ConnectionId is not null;
    public string? AvatarUrl => AvatarPng is null ? null : $"/avatar/{Id}?v={AvatarVersion}";
}

// ---------- DTOs sent to clients ----------

public sealed record PlayerSummary(
    Guid Id, string Nickname, string? AvatarUrl, int Score, int Rank, int PrevRank, int Gain, int Bonus, int StreakBonus, int Streak, int Seat,
    bool Connected, bool Answered);

public sealed record PublicQuestion(
    QuestionType Type,
    string Text,
    string? Image,
    IReadOnlyList<string> Options,
    IReadOnlyList<QuizItem> GroupLeft,
    IReadOnlyList<QuizItem> GroupRight,
    string? Unit,
    /// <summary>Ordering: the items shuffled (the starting order on every phone) and the notes for the two ends.</summary>
    IReadOnlyList<QuizItem> OrderItems,
    string? TopLabel,
    string? BottomLabel,
    /// <summary>MultipleChoice: a picture per option, or empty.</summary>
    IReadOnlyList<string?> OptionImages);

public sealed record ClosestGuess(string Nickname, string? AvatarUrl, int Seat, double Guess, int Points);

public sealed record RevealInfo(
    int CorrectIndex,
    int[] OptionCounts,
    double? CorrectNumber,
    IReadOnlyList<ClosestGuess> ClosestGuesses,
    IReadOnlyList<double> AllGuesses,
    IReadOnlyList<GroupPair> CorrectPairs,
    /// <summary>Ordering: the items in the right order, and how many players put each one in its right place.</summary>
    IReadOnlyList<QuizItem> CorrectOrder,
    int[] PlacedRight,
    IReadOnlyList<PlayerSummary> FirstCorrect,
    int CorrectPlayers,
    int AnsweredPlayers,
    /// <summary>The question's optional explanation, shown with the correct answer.</summary>
    string? Explanation = null,
    string? ExplanationImage = null);

/// <summary>An end-of-game award, e.g. "Fastest finger".</summary>
public sealed record Award(string Key, string Title, string Blurb, Guid WinnerId, string Stat);

/// <summary>An award with its winner resolved for display.</summary>
public sealed record AwardView(string Key, string Title, string Blurb, PlayerSummary Winner, string Stat);

public sealed record AdminView(
    Phase Phase,
    string QuizTitle,
    int QuestionIndex,
    int QuestionCount,
    int RemainingMs,
    int TotalMs,
    IReadOnlyList<PlayerSummary> Players,
    PublicQuestion? Question,
    int AnsweredCount,
    RevealInfo? Reveal,
    IReadOnlyList<PlayerSummary> Leaderboard,
    IReadOnlyList<AwardView> Awards,
    string? JoinUrl,
    bool Paused,
    bool Persistent);

public sealed record PlayerView(
    Phase Phase,
    PlayerSummary Me,
    int PlayerCount,
    int QuestionIndex,
    int QuestionCount,
    int RemainingMs,
    int TotalMs,
    PublicQuestion? Question,
    bool Answered,
    int? MyChoice,
    int[]? MyGrouping,
    double? MyNumber,
    QuestionResult? Result,
    string? CorrectAnswer,
    string? Boost,
    PlayerSummary? BoostRival,
    IReadOnlyList<AwardView> MyAwards,
    bool Paused,
    IReadOnlyList<RecapItem> Recap,
    string? Explanation = null,
    string? ExplanationImage = null);

/// <summary>One line of a player's end-of-game recap: their answer next to the right one.</summary>
public sealed record RecapItem(int Number, string Question, string YourAnswer, string? CorrectAnswer, int Points, bool Correct, bool Answered);
