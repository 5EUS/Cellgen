using Cellgen.Generation;

namespace Cellgen.Rules;

/// <summary>The outcome of reading a rule set.</summary>
/// <param name="Set">The normalized set, or null when the document was unusable.</param>
/// <param name="Diagnostics">Everything that was wrong with it, by path.</param>
public sealed record RuleParseResult(RuleSet? Set, IReadOnlyList<GenDiagnostic> Diagnostics);
