namespace Qvizz.Game;

/// <summary>
/// Random encouraging messages shown on the player's phone after each question.
/// After the last question nothing can change any more, so a separate set looks back instead of ahead.
/// </summary>
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

    // ----- after the last question -----

    private static readonly string[] FinalFirst =
    [
        "You finished first. What a game!",
        "Top of the table at the final whistle!",
        "Number one when it counted!"
    ];

    private static readonly string[] FinalFirstWithLead =
    [
        "You finished first, {gap} points ahead of {name}!",
        "First place, {gap} points clear of {name}. Well played!"
    ];

    private static readonly string[] FinalSharedFirst =
    [
        "You share first place with {name}!",
        "A dead heat at the top with {name}!"
    ];

    private static readonly string[] FinalBehind =
    [
        "You finished just {gap} points behind {name}. So close!",
        "Only {gap} points between you and {name} at the end!",
        "{name} edged you by {gap} points. Rematch?"
    ];

    private static readonly string[] FinalTied =
    [
        "You finished level with {name}!",
        "Dead even with {name} at the finish!"
    ];

    private static readonly string[] FinalNoPoints =
    [
        "Thanks for playing! Next time it's yours.",
        "Not your night, but what a game!"
    ];

    /// <summary>Returns the message and the rival it mentions (shown as a face next to it), if any.</summary>
    public static (string Text, PlayerState? Rival) For(int rank, int score, PlayerState? above, PlayerState? below,
        PlayerState? tiedWith, bool final = false)
    {
        (string, PlayerState?) Fill(string[] pool, PlayerState? other, int gap) =>
            (Pick(pool).Replace("{name}", other?.Nickname ?? "")
                .Replace("{gap} points", gap == 1 ? "1 point" : $"{gap} points")
                .Replace("{gap}", gap.ToString()), other);

        if (score == 0 && rank > 1) return (Pick(final ? FinalNoPoints : NoPoints), null);
        if (tiedWith is not null)
            return Fill(rank == 1 ? (final ? FinalSharedFirst : SharedFirst) : (final ? FinalTied : Tied), tiedWith, 0);
        if (rank == 1)
        {
            return below is not null && Random.Shared.Next(2) == 0
                ? Fill(final ? FinalFirstWithLead : FirstWithLead, below, score - below.Score)
                : (Pick(final ? FinalFirst : First), null);
        }
        return Fill(final ? FinalBehind : Behind, above, above!.Score - score);
    }

    private static string Pick(string[] pool) => pool[Random.Shared.Next(pool.Length)];
}
