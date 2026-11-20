using Qvizz.Quiz;

namespace Qvizz.Game;

/// <summary>
/// End-of-game awards, computed from each player's answer history.
/// An award only appears when someone actually earned it.
/// </summary>
public static class Awards
{
    public static IReadOnlyList<Award> Compute(IReadOnlyCollection<PlayerState> players, QuizDefinition quiz)
    {
        var list = new List<Award>();
        if (players.Count == 0) return list;

        // Each award collects everyone who qualifies, best first; winners are assigned at the end
        // so the awards spread around the table instead of the champion sweeping them all.
        var pending = new List<(string key, string title, string blurb, List<(PlayerState p, double v)> ranked,
            List<(PlayerState p, double v)> fallback, Func<PlayerState, double, string> stat)>();

        void Add(string key, string title, string blurb, Func<PlayerState, double> metric, double minimum,
            Func<PlayerState, double, string> stat, bool lowestWins = false)
        {
            var qualified = players.Select(p => (p, v: metric(p)))
                .Where(x => !double.IsNaN(x.v) && (lowestWins ? x.v <= minimum : x.v >= minimum)).ToList();
            if (qualified.Count == 0) return;
            var ranked = (lowestWins
                ? qualified.OrderBy(x => x.v).ThenByDescending(x => x.p.Score).ThenBy(x => x.p.Seat)
                : qualified.OrderByDescending(x => x.v).ThenByDescending(x => x.p.Score).ThenBy(x => x.p.Seat)).ToList();
            // Contenders: within reach of the best (a tie, a close second, or one short on a count).
            var best = ranked[0].v;
            var close = ranked.Where(x => lowestWins
                ? x.v <= best * 1.5 + 0.02
                : x.v >= best * 0.6 || best - x.v <= 1).ToList();
            // Further back, but still a fair holder of the title when the leader already has enough trophies.
            var fallback = ranked.Where(x => lowestWins ? x.v <= best * 2 + 0.05 : x.v >= best * 0.5).ToList();
            pending.Add((key, title, blurb, close, fallback, stat));
        }

        var numberQs = quiz.Questions.Select((q, i) => (q, i)).Where(x => x.q.Type == QuestionType.ClosestNumber).ToList();
        double RelError(double guess, double answer) =>
            answer == 0 ? Math.Abs(guess) : Math.Abs(guess - answer) / Math.Abs(answer);

        // 1. Fastest finger: most speed-bonus points.
        Add("fastest", "Fastest finger", "Always first to the button",
            p => p.History.Values.Sum(r => r.Bonus), 1,
            (_, v) => $"+{v:0} speed bonus points");

        // 2. Biggest comeback: furthest climb from their lowest point to the final rank.
        Add("comeback", "Biggest comeback", "Never count them out",
            p =>
            {
                var ranks = p.History.Values.OrderBy(r => r.QuestionIndex).Select(r => r.RankAfter).ToList();
                return ranks.Count < 2 ? double.NaN : ranks.Take(ranks.Count - 1).Max() - p.Rank;
            }, 2,
            (p, v) => $"from #{p.Rank + (int)v} to #{p.Rank}");

        // 3. Closest guesser: smallest average error on the closest-number questions.
        //    Only when the quiz has at least 3 of them, so one lucky guess doesn't make a winner.
        if (numberQs.Count >= 3)
            Add("closest", "Closest guesser", "Eerily good with numbers",
                p =>
                {
                    var errors = numberQs.Where(x => p.History.TryGetValue(x.i, out var r) && r.Number is not null)
                        .Select(x => RelError(p.History[x.i].Number!.Value, x.q.CorrectNumber!.Value)).ToList();
                    return errors.Count == 0 ? double.NaN : errors.Average();
                }, double.MaxValue,
                (_, v) => v < 0.005 ? "spot on" : $"off by {Pct(v)} on average", lowestWins: true);

        // 4. Sharpshooter: most correct answers.
        Add("sharp", "Sharpshooter", "Rarely misses",
            p => p.History.Values.Count(r => r.Correct), 1,
            (_, v) => $"{v:0} of {quiz.Questions.Count} right");

        // 5. Last-second hero: at least 4 answers locked in during the final 3 seconds.
        Add("lastsecond", "Last-second hero", "Lives dangerously",
            p => p.History.Values.Count(r => r.Answered && r.RemainingMs is <= 3000), 4,
            (_, v) => $"{v:0} answers in the final 3 seconds");

        // Hand out the awards so they spread around the table:
        // among the close contenders the one with the fewest awards wins (ties go to the better performer);
        // nobody takes more than two — then a fair runner-up without an award gets it, or the award is skipped.
        const int maxPerPlayer = 2;
        var wins = new Dictionary<Guid, int>();
        foreach (var (key, title, blurb, close, fallback, stat) in pending)
        {
            var (p, v) = close.Select((x, i) => (x, i))
                .OrderBy(t => wins.GetValueOrDefault(t.x.p.Id)).ThenBy(t => t.i).First().x;
            if (wins.GetValueOrDefault(p.Id) >= maxPerPlayer)
            {
                (p, v) = fallback.FirstOrDefault(x => wins.GetValueOrDefault(x.p.Id) == 0);
                if (p is null) continue;
            }
            wins[p.Id] = wins.GetValueOrDefault(p.Id) + 1;
            list.Add(new Award(key, title, blurb, p.Id, stat(p, v)));
        }
        return list;
    }

    private static string Pct(double v) => v >= 0.1 ? $"{v * 100:0}%" : $"{v * 100:0.#}%";
}
