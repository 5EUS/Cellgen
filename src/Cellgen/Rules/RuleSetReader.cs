using System.Text.Json;

using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>
/// Reads and writes the JSON form of a <see cref="RuleSet"/>, the contract between Cellgen and whatever
/// produces rules. Keys are snake_case; unknown keys are errors, so a typo is reported, not ignored.
///
/// <code>
/// { "version": 1,
///   "rules": [
///     { "target": "encounter", "match": { "tag": "boss", "max_depth": 2 }, "allow": false },
///     { "target": "cell", "match": { "tags_any": ["trench"], "level": "frontline_01" }, "weight": 2.5 }
///   ],
///   "budgets": { "encounter": 0.5, "loot": 2 } }
/// </code>
/// </summary>
public static class RuleSetReader
{
    /// <summary>Reads a rule set. Never throws: problems come back as diagnostics.</summary>
    /// <param name="json">The JSON document.</param>
    /// <returns>The normalized set (null when unusable) and the diagnostics.</returns>
    public static RuleParseResult Parse(string json)
    {
        RuleSet? written;
        try
        {
            written = JsonSerializer.Deserialize(json, RuleSetJsonContext.Default.RuleSet);
        }
        catch (JsonException exception)
        {
            return new RuleParseResult(null, [new GenDiagnostic(GenSeverity.Error, "$", exception.Message)]);
        }

        if (written is null)
        {
            return new RuleParseResult(null, [new GenDiagnostic(GenSeverity.Error, "$", "the document is empty")]);
        }

        RuleSet? normalized = written.Normalize(out IReadOnlyList<GenDiagnostic> diagnostics);
        return new RuleParseResult(normalized, diagnostics);
    }

    /// <summary>
    /// Reads several documents (one per script, say) and merges them into one set: rules in the order
    /// given, budget multipliers multiplied. A document that cannot be used is reported and left out;
    /// the rest still apply.
    /// </summary>
    /// <param name="documents">The JSON documents, in the order their rules should apply.</param>
    /// <returns>The merged set, or null when no document was usable, and every diagnostic by document.</returns>
    public static RuleParseResult ParseMany(IReadOnlyList<string> documents)
    {
        var diagnostics = new List<GenDiagnostic>();
        var rules = new List<Rule>();
        float? encounter = null;
        float? loot = null;
        bool any = false;
        for (int i = 0; i < documents.Count; i++)
        {
            RuleParseResult one = Parse(documents[i]);
            diagnostics.AddRange(one.Diagnostics.Select(d => d with { Path = $"documents[{i}].{d.Path}" }));
            if (one.Set is not { } set)
            {
                continue;
            }

            any = true;
            rules.AddRange(set.Rules);
            encounter = Multiply(encounter, set.Budgets?.Encounter);
            loot = Multiply(loot, set.Budgets?.Loot);
        }

        RuleBudgets? budgets = (encounter is null && loot is null) ? null : new RuleBudgets { Encounter = encounter, Loot = loot };
        RuleSet? merged = any ? new RuleSet { Rules = rules, Budgets = budgets } : null;
        return new RuleParseResult(merged, diagnostics);
    }

    /// <summary>Writes a rule set as JSON.</summary>
    /// <param name="set">The set.</param>
    /// <returns>The document.</returns>
    public static string ToJson(RuleSet set) => JsonSerializer.Serialize(set, RuleSetJsonContext.Default.RuleSet);

    private static float? Multiply(float? total, float? scale) => (total, scale) switch
    {
        (null, _) => scale,
        (_, null) => total,
        _ => total * scale,
    };
}
