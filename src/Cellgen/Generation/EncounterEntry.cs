namespace Cellgen.Generation;

/// <summary>One encounter the populator can assign to a zone.</summary>
/// <param name="Id">The encounter's content id.</param>
/// <param name="Cost">What it spends from the level's budget.</param>
/// <param name="Tags">Its tags.</param>
/// <param name="MinDepth">The first run depth it may appear at.</param>
/// <param name="MaxDepth">The last; -1 for no limit.</param>
public sealed record EncounterEntry(string Id, float Cost, IReadOnlyList<string> Tags, int MinDepth, int MaxDepth)
{
    /// <summary>
    /// The reserved tag of a boss encounter: it is drawn only by a level whose
    /// <c>EncounterTags</c> name it, so a boss never turns up in an ordinary level.
    /// </summary>
    public const string BossTag = "boss";

    /// <summary>Gets a value indicating whether the encounter carries <see cref="BossTag"/>.</summary>
    public bool IsBoss => this.Tags.Contains(BossTag, StringComparer.Ordinal);

    /// <summary>Gets whether the encounter may appear at a depth.</summary>
    /// <param name="depth">The run depth.</param>
    /// <returns>True when within range.</returns>
    public bool AllowsDepth(int depth) => depth >= this.MinDepth && (this.MaxDepth < 0 || depth <= this.MaxDepth);

    /// <summary>Gets whether the encounter carries any of <paramref name="tags"/>; true for an empty list.</summary>
    /// <param name="tags">The tags.</param>
    /// <returns>True on any match.</returns>
    public bool HasAnyTag(IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return true;
        }

        return tags.Any(tag => this.Tags.Contains(tag, StringComparer.Ordinal));
    }
}
