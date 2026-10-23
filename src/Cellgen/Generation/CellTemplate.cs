namespace Cellgen.Generation;

/// <summary>A baked cell as the generator uses it: its rules, and its distinct rotations.</summary>
internal sealed class CellTemplate
{
    private readonly HashSet<string> tags;

    /// <summary>Initializes a new instance of the <see cref="CellTemplate"/> class.</summary>
    /// <param name="info">The baked cell.</param>
    /// <param name="weight">Its weight, already multiplied by its set's.</param>
    /// <param name="rotationMask">Which quarter turns to build variants for, as bits 0-3.</param>
    public CellTemplate(CellData info, float weight, int rotationMask)
    {
        this.Info = info;
        this.Weight = weight;
        this.Footprint = info.Footprint;
        this.tags = new HashSet<string>(info.Tags, StringComparer.Ordinal);

        // Distinct rotations only: a crossroads looks the same four ways, and
        // without merging it would be tried (and picked) four times as often
        // as an asymmetric cell of the same weight.
        var variants = new List<CellVariant>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int r = 0; r < 4; r++)
        {
            if ((rotationMask & (1 << r)) == 0)
            {
                continue;
            }

            var variant = new CellVariant(this, r);
            if (seen.Add(variant.Signature()))
            {
                variants.Add(variant);
            }
        }

        foreach (CellVariant variant in variants)
        {
            variant.Weight = weight / variants.Count;
        }

        this.Variants = variants;
        this.Name = Path.GetFileNameWithoutExtension(info.ScenePath);
    }

    /// <summary>Gets the baked cell.</summary>
    public CellData Info { get; }

    /// <summary>Gets a short name for diagnostics and the preview: the scene's file name.</summary>
    public string Name { get; }

    /// <summary>Gets the authored footprint.</summary>
    public Int3 Footprint { get; }

    /// <summary>Gets the weight.</summary>
    public float Weight { get; }

    /// <summary>Gets the distinct rotations.</summary>
    public IReadOnlyList<CellVariant> Variants { get; }

    /// <summary>Gets whether the cell carries a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>True when present.</returns>
    public bool HasTag(string tag) => this.tags.Contains(tag);

    /// <summary>Gets whether the cell may appear at a depth.</summary>
    /// <param name="depth">The run depth.</param>
    /// <returns>True when within the cell's range.</returns>
    public bool AllowsDepth(int depth) => depth >= this.Info.MinDepth && (this.Info.MaxDepth < 0 || depth <= this.Info.MaxDepth);
}
