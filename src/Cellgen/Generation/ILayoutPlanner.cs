namespace Cellgen.Generation;

/// <summary>
/// Lays out a level's slots: the first stage of generation, before any cell is
/// chosen. A planner turns a <see cref="LayoutRequest"/> into a
/// <see cref="MacroPlan"/> (which slots are used and why, which must connect,
/// where the start and exit are), and the cell solver and populator do the rest
/// from that plan alone. Register one in a <see cref="LayoutRegistry"/> and
/// name it in <see cref="LevelData.LayoutId"/>.
///
/// <para>
/// Determinism is the planner's contract, and nothing enforces it: draw every
/// random number from <see cref="LayoutRequest.Rng"/>, never from the clock,
/// <see cref="System.Random"/> or a hash-ordered collection, and the same recipe,
/// seed and depth give the same level everywhere. The golden-fingerprint test
/// pattern in this repository is how to check a planner holds it.
/// </para>
/// </summary>
public interface ILayoutPlanner
{
    /// <summary>Gets the id recipes name in <see cref="LevelData.LayoutId"/>. Lowercase by convention.</summary>
    string Id { get; }

    /// <summary>Plans one attempt's layout.</summary>
    /// <param name="request">The recipe, context, random stream and report.</param>
    /// <param name="failure">Why no plan was made, when null is returned. The generator retries on a derived seed.</param>
    /// <returns>The plan, or null when this attempt fails.</returns>
    MacroPlan? Plan(LayoutRequest request, out string failure);
}
