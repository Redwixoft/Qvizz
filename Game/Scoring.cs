namespace Qvizz.Game;

public static class Scoring
{
    public const int MultipleChoicePoints = 100;
    public const int TrueFalsePoints = 80;
    public const int GroupingPointsPerPair = 20;
    /// <summary>Ordering: points for each item in its right place.</summary>
    public const int OrderingPointsPerItem = 20;

    /// <summary>Extra points per question while a player is on a streak of this many or more.</summary>
    public const int StreakBonus = 5;
    public const int StreakFrom = 3;

    /// <summary>Matching and ordering keep the streak with at least half the pieces right (2 of 4, 3 of 5).</summary>
    public static bool KeepsStreakOnMatching(int goodPairs, int pairs) => goodPairs >= (pairs + 1) / 2;

    /// <summary>Closest-number keeps the streak for the better half of all players (at least the closest one).</summary>
    public static bool KeepsStreakOnNumber(int place, int players) => place < Math.Max(1, players / 2);

    /// <summary>Speed bonus for the n-th (0-based) correct guesser: 20, 18, 16, 15, 14, ... 1, then 0.</summary>
    public static int SpeedBonus(int position) => position switch
    {
        0 => 20,
        1 => 18,
        2 => 16,
        _ => Math.Max(0, 15 - (position - 3))
    };

    /// <summary>Points for the n-th (0-based) closest guess: 100, 90, 80, 70, 65, 60, ... 5, then 0.</summary>
    public static int ClosestNumberPoints(int rank) => rank switch
    {
        0 => 100,
        1 => 90,
        2 => 80,
        _ => Math.Max(0, 70 - 5 * (rank - 3))
    };
}
