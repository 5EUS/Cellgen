namespace Cellgen.Generation;

/// <summary>What <see cref="LevelGenerator.Generate"/> produced.</summary>
/// <param name="Plan">The level, or null when no attempt succeeded and there was no fallback.</param>
/// <param name="Report">Everything the generator had to say.</param>
public sealed record GenResult(LevelPlan? Plan, GenReport Report);
