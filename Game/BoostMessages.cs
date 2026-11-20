namespace Qvizz.Game;

/// <summary>Random encouraging messages shown on the player's phone after each question.</summary>
public static class BoostMessages
{
    private static readonly string[] First =
    [
        "You are first, awesome!",
        "Top of the table! Keep it up!",
        "Number one! Everyone is chasing you now.",
        "You're leading the pack!",
        "Gold position! Don't look back."
    ];

    private static readonly string[] FirstWithLead =
    [
        "You're first, {gap} points ahead of {name}!",
        "Leading by {gap} points. {name} is breathing down your neck!",
        "{gap} points clear of {name}. Stay sharp!"
    ];

    private static readonly string[] SharedFirst =
    [
        "You're tied for first place with {name}!",
        "Dead heat at the top with {name}!"
    ];

    private static readonly string[] Behind =
    [
        "You are only {gap} points behind {name}!",
        "Just {gap} points to catch {name}!",
        "{name} is only {gap} points ahead. Go get them!",
        "One good answer and you pass {name} ({gap} points)!",
        "{gap} points to overtake {name}. You've got this!"
    ];

    private static readonly string[] Tied =
    [
        "You're neck and neck with {name}!",
        "Tied with {name}. Time to break the tie!"
    ];

    private static readonly string[] NoPoints =
    [
        "Warming up! The next one is yours.",
        "Every champion starts somewhere!",
        "Plenty of questions left. Stay in the game!"
    ];

    /// <summary>Returns the message and the rival it mentions (shown as a face next to it), if any.</summary>
    public static (string Text, PlayerState? Rival) For(int rank, int score, PlayerState? above, PlayerState? below, PlayerState? tiedWith)
    {
        (string, PlayerState?) Fill(string[] pool, PlayerState? other, int gap) =>
            (Pick(pool).Replace("{name}", other?.Nickname ?? "")
                .Replace("{gap} points", gap == 1 ? "1 point" : $"{gap} points")
                .Replace("{gap}", gap.ToString()), other);

        if (score == 0 && rank > 1) return (Pick(NoPoints), null);
        if (tiedWith is not null) return Fill(rank == 1 ? SharedFirst : Tied, tiedWith, 0);
        if (rank == 1)
        {
            return below is not null && Random.Shared.Next(2) == 0
                ? Fill(FirstWithLead, below, score - below.Score)
                : (Pick(First), null);
        }
        return Fill(Behind, above, above!.Score - score);
    }

    private static string Pick(string[] pool) => pool[Random.Shared.Next(pool.Length)];
}
