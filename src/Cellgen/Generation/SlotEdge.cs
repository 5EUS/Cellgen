using System.Runtime.InteropServices;

namespace Cellgen.Generation;

/// <summary>
/// An undirected link between two neighbouring slots that must end up
/// connected. Normalized so <c>(a, b)</c> and <c>(b, a)</c> are equal.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SlotEdge
{
    /// <summary>Initializes a new instance of the <see cref="SlotEdge"/> struct.</summary>
    /// <param name="a">One slot.</param>
    /// <param name="b">The other.</param>
    public SlotEdge(Int3 a, Int3 b)
    {
        bool ordered = a.X < b.X || (a.X == b.X && (a.Y < b.Y || (a.Y == b.Y && a.Z <= b.Z)));
        this.A = ordered ? a : b;
        this.B = ordered ? b : a;
    }

    /// <summary>Gets the lesser slot.</summary>
    public Int3 A { get; }

    /// <summary>Gets the greater slot.</summary>
    public Int3 B { get; }
}
