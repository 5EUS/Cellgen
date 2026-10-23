namespace Cellgen.Generation;

/// <summary>
/// Makes a policy for one generation. Where an <see cref="IGenerationPolicy"/> in
/// <see cref="GenContext.Policies"/> is shared by every level, a source is asked
/// once per recipe attempted (the fallback recipe asks again), so what it makes can
/// hold state for exactly that generation: a script session, a compiled rule
/// set. What the scope was given to run on ending (disposing a script session,
/// say) runs when the generation ends, however it ends.
/// </summary>
public interface IGenerationPolicySource
{
    /// <summary>Makes the policy for one recipe's generation.</summary>
    /// <param name="def">The recipe about to be generated.</param>
    /// <param name="context">The seed, depth, facts and catalogs.</param>
    /// <param name="report">Where to put warnings, such as a script that failed; they reach the editor preview and the logs.</param>
    /// <returns>The policy and its cleanup, or null when this source has nothing to say about this recipe.</returns>
    PolicyScope? Begin(LevelData def, GenContext context, GenReport report);
}
