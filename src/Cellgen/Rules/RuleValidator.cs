using System.Globalization;

using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>Checks a <see cref="RuleSet"/> and returns a clean copy. Structural problems reject the set; a bad rule is dropped on its own, so one typo cannot silence the rest.</summary>
internal static class RuleValidator
{
    /// <summary>Normalizes a rule set.</summary>
    /// <param name="set">The set as written.</param>
    /// <param name="diagnostics">Every problem found.</param>
    /// <returns>The clean copy, or null when the set as a whole is unusable.</returns>
    internal static RuleSet? Normalize(RuleSet set, out IReadOnlyList<GenDiagnostic> diagnostics)
    {
        var found = new List<GenDiagnostic>();
        diagnostics = found;
        if (set.Version != RuleSet.CurrentVersion)
        {
            found.Add(new GenDiagnostic(GenSeverity.Error, "version", $"rule set version {set.Version} is not supported; this library reads version {RuleSet.CurrentVersion}"));
            return null;
        }

        var rules = new List<Rule>();
        IReadOnlyList<Rule> written = set.Rules ?? [];
        for (int i = 0; i < written.Count; i++)
        {
            if (NormalizeRule(written[i], $"rules[{i}]", found) is { } rule)
            {
                rules.Add(rule);
            }
        }

        return new RuleSet { Version = RuleSet.CurrentVersion, Rules = rules, Budgets = NormalizeBudgets(set.Budgets, found) };
    }

    private static Rule? NormalizeRule(Rule? rule, string path, List<GenDiagnostic> found)
    {
        if (rule is null)
        {
            found.Add(Warn(path, "is empty; dropped"));
            return null;
        }

        if (!RuleTargets.All.Contains(rule.Target, StringComparer.Ordinal))
        {
            found.Add(Warn($"{path}.target", $"'{rule.Target}' is not a target (use {string.Join(", ", RuleTargets.All)}); rule dropped"));
            return null;
        }

        RuleMatch match = rule.Match ?? new RuleMatch();
        if (CheckMatch(match, rule.Target, $"{path}.match", found) is not { } checkedMatch)
        {
            return null;
        }

        float? weight = NormalizeWeight(rule, path, found);
        bool? allow = (rule.Allow == false) ? false : null;
        if (rule.Allow == true && weight is null)
        {
            found.Add(Warn($"{path}.allow", "true changes nothing; use false to forbid"));
        }

        if (allow is null && weight is null)
        {
            found.Add(Warn(path, "has no effect (set allow to false or give a weight); dropped"));
            return null;
        }

        return new Rule { Target = rule.Target, Match = checkedMatch, Allow = allow, Weight = weight };
    }

    private static RuleMatch? CheckMatch(RuleMatch match, string target, string path, List<GenDiagnostic> found)
    {
        int idForms = (match.Id is null ? 0 : 1) + (match.Ids is null ? 0 : 1) + (match.IdPrefix is null ? 0 : 1);
        if (idForms > 1)
        {
            found.Add(Warn(path, "sets more than one of id, ids and id_prefix; rule dropped"));
            return null;
        }

        if (match.Id is { Length: 0 } || match.IdPrefix is { Length: 0 } || match.Ids is { Count: 0 } || match.TagsAny is { Count: 0 } || match.TagsAll is { Count: 0 })
        {
            found.Add(Warn(path, "has an empty id, prefix or list, which matches nothing; rule dropped"));
            return null;
        }

        if (match.MinDepth > match.MaxDepth)
        {
            found.Add(Warn(path, $"min_depth {match.MinDepth} is above max_depth {match.MaxDepth}, which matches nothing; rule dropped"));
            return null;
        }

        bool tagged = match.Tag is not null || match.TagsAny is not null || match.TagsAll is not null;
        if (tagged && string.Equals(target, RuleTargets.Structure, StringComparison.Ordinal))
        {
            found.Add(Warn(path, "matches structures by tag, but structures have no tags; rule dropped"));
            return null;
        }

        return match;
    }

    private static float? NormalizeWeight(Rule rule, string path, List<GenDiagnostic> found)
    {
        if (rule.Weight is not { } weight)
        {
            return null;
        }

        bool drawn = rule.Target is RuleTargets.Cell or RuleTargets.Encounter or RuleTargets.Loot;
        if (!drawn)
        {
            found.Add(Warn($"{path}.weight", $"{rule.Target}s are not drawn by weight; ignored (use allow)"));
            return null;
        }

        if (!float.IsFinite(weight))
        {
            found.Add(Warn($"{path}.weight", "is not a finite number; ignored"));
            return null;
        }

        if (weight < 0f || weight > RuleSet.MaxWeight)
        {
            float clamped = Math.Clamp(weight, 0f, RuleSet.MaxWeight);
            found.Add(Warn($"{path}.weight", string.Create(CultureInfo.InvariantCulture, $"{weight} is outside 0..{RuleSet.MaxWeight}; clamped to {clamped}")));
            return clamped;
        }

        return weight;
    }

    private static RuleBudgets? NormalizeBudgets(RuleBudgets? budgets, List<GenDiagnostic> found)
    {
        if (budgets is null)
        {
            return null;
        }

        return new RuleBudgets
        {
            Encounter = NormalizeScale(budgets.Encounter, "budgets.encounter", found),
            Loot = NormalizeScale(budgets.Loot, "budgets.loot", found),
        };
    }

    private static float? NormalizeScale(float? scale, string path, List<GenDiagnostic> found)
    {
        if (scale is not { } value)
        {
            return null;
        }

        if (!float.IsFinite(value))
        {
            found.Add(Warn(path, "is not a finite number; ignored"));
            return null;
        }

        float clamped = Math.Clamp(value, RuleSet.MinBudgetScale, RuleSet.MaxBudgetScale);
        if (value < RuleSet.MinBudgetScale || value > RuleSet.MaxBudgetScale)
        {
            found.Add(Warn(path, string.Create(CultureInfo.InvariantCulture, $"{value} is outside {RuleSet.MinBudgetScale}..{RuleSet.MaxBudgetScale}; clamped to {clamped}")));
        }

        return clamped;
    }

    private static GenDiagnostic Warn(string path, string message) => new(GenSeverity.Warning, path, message);
}
