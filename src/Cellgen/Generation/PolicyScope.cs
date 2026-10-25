namespace Cellgen.Generation;

/// <summary>The policies made for one generation, and what to do when that generation ends.</summary>
/// <remarks>Initializes a new instance of the <see cref="PolicyScope"/> class.</remarks>
/// <param name="policies">The policies, applied in this order.</param>
/// <param name="end">Runs once when the generation ends, however it ends; null for nothing to clean up.</param>
public sealed class PolicyScope(IReadOnlyList<IGenerationPolicy> policies, Action? end = null)
{
    private readonly Action? end = end;

    /// <summary>Initializes a new instance of the <see cref="PolicyScope"/> class.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="end">Runs once when the generation ends, however it ends; null for nothing to clean up.</param>
    public PolicyScope(IGenerationPolicy policy, Action? end = null)
        : this([policy], end)
    {
    }

    /// <summary>Gets the policies.</summary>
    public IReadOnlyList<IGenerationPolicy> Policies { get; } = policies;

    /// <summary>Runs the end action.</summary>
    internal void End() => this.end?.Invoke();
}
