using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qvizz.Quiz;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionType
{
    MultipleChoice,
    TrueFalse,
    Grouping,
    ClosestNumber,
    /// <summary>Put the items in the right order, top to bottom.</summary>
    Ordering
}

/// <summary>Text and/or image. Used for grouping items.</summary>
public sealed class QuizItem
{
    public string? Text { get; set; }
    /// <summary>Path relative to wwwroot, e.g. "/images/cat.jpg".</summary>
    public string? Image { get; set; }
}

public sealed class GroupPair
{
    public QuizItem Left { get; set; } = new();
    public QuizItem Right { get; set; } = new();
}

public sealed class Question
{
    public QuestionType Type { get; set; }
    public string Text { get; set; } = "";
    public string? Image { get; set; }

    /// <summary>Optional: a few words on why the answer is right, shown with the correct answer (big screen and phones).</summary>
    public string? Explanation { get; set; }
    /// <summary>Optional: a picture shown with the correct answer, in place of the question's picture.</summary>
    public string? ExplanationImage { get; set; }

    /// <summary>Optional override of the default time limit.</summary>
    public int? TimeLimitSeconds { get; set; }

    // MultipleChoice: exactly 4 options. TrueFalse: ignored (always True/False).
    public List<string> Options { get; set; } = [];

    /// <summary>MultipleChoice: optional picture for each option (same order as options), e.g. a colleague's face.</summary>
    public List<string?> OptionImages { get; set; } = [];

    // MultipleChoice: 0..3. TrueFalse: use CorrectBool.
    public int? CorrectIndex { get; set; }
    public bool? CorrectBool { get; set; }

    // Grouping: 4 or 5 pairs, each Left belongs with its Right.
    public List<GroupPair> Pairs { get; set; } = [];

    // Ordering: 3-6 items listed in the RIGHT order, top to bottom (players get them shuffled).
    public List<QuizItem> Items { get; set; } = [];
    /// <summary>Ordering: notes shown beside the list's two ends, e.g. "Longest in the company" / "Newest".</summary>
    public string? TopLabel { get; set; }
    public string? BottomLabel { get; set; }

    // ClosestNumber
    public double? CorrectNumber { get; set; }
    public string? Unit { get; set; }

    /// <summary>Options as shown to players (TrueFalse gets fixed True/False).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> EffectiveOptions =>
        Type == QuestionType.TrueFalse ? ["True", "False"] : Options;

    /// <summary>How many pieces players move around: grouping pairs or ordering items.</summary>
    [JsonIgnore]
    public int Pieces => Type switch { QuestionType.Grouping => Pairs.Count, QuestionType.Ordering => Items.Count, _ => 0 };

    [JsonIgnore]
    public int EffectiveCorrectIndex =>
        Type == QuestionType.TrueFalse ? (CorrectBool == true ? 0 : 1) : CorrectIndex ?? -1;
}

public sealed class QuizDefinition
{
    /// <summary>The quiz name shown in the lobby, e.g. "Christmas Party Quiz 2026". ("title" is accepted too.)</summary>
    public string? Name { get; set; }
    public string? Title { get; set; }

    [JsonIgnore]
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name.Trim()
        : !string.IsNullOrWhiteSpace(Title) ? Title.Trim() : "Qvizz";
    public List<Question> Questions { get; set; } = [];

    /// <summary>Christmas look on the big screen and phones: snow, Santa hats, garland, gifts, holiday ticker.</summary>
    public bool Festive { get; set; }

    public static QuizDefinition Load(string path)
    {
        var json = File.ReadAllText(path);
        var quiz = JsonSerializer.Deserialize<QuizDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException("quiz.json is empty");
        quiz.Validate();
        return quiz;
    }

    private void Validate()
    {
        for (var i = 0; i < Questions.Count; i++)
        {
            var q = Questions[i];
            var where = $"Question #{i + 1} ({q.Type})";
            switch (q.Type)
            {
                case QuestionType.MultipleChoice:
                    if (q.Options.Count != 4) throw new InvalidOperationException($"{where}: needs exactly 4 options");
                    if (q.CorrectIndex is not (>= 0 and <= 3)) throw new InvalidOperationException($"{where}: correctIndex must be 0-3");
                    if (q.OptionImages.Count is not (0 or 4)) throw new InvalidOperationException($"{where}: optionImages needs 4 pictures (or none)");
                    break;
                case QuestionType.TrueFalse:
                    if (q.CorrectBool is null) throw new InvalidOperationException($"{where}: correctBool is required");
                    break;
                case QuestionType.Grouping:
                    if (q.Pairs.Count is < 2 or > 6) throw new InvalidOperationException($"{where}: needs 2-6 pairs");
                    break;
                case QuestionType.Ordering:
                    if (q.Items.Count is < 3 or > 6) throw new InvalidOperationException($"{where}: needs 3-6 items");
                    break;
                case QuestionType.ClosestNumber:
                    if (q.CorrectNumber is null) throw new InvalidOperationException($"{where}: correctNumber is required");
                    break;
            }
        }
    }
}
