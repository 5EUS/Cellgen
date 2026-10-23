using System.Globalization;
using System.Runtime.InteropServices;

namespace Cellgen;

/// <summary>
/// An integer triple: a slot in the level grid, or a footprint in slots. X and
/// Z are horizontal, Y is the layer.
/// </summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Int3(int X, int Y, int Z)
{
    /// <summary>Gets <c>(0, 0, 0)</c>.</summary>
    public static Int3 Zero => new(0, 0, 0);

    /// <summary>Gets <c>(1, 1, 1)</c>.</summary>
    public static Int3 One => new(1, 1, 1);

    /// <summary>Gets <c>(0, 1, 0)</c>.</summary>
    public static Int3 Up => new(0, 1, 0);

    /// <summary>Adds two triples per component.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The sum.</returns>
    public static Int3 operator +(Int3 a, Int3 b) => Add(a, b);

    /// <summary>Subtracts two triples per component.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The difference.</returns>
    public static Int3 operator -(Int3 a, Int3 b) => Subtract(a, b);

    /// <summary>Negates a triple per component.</summary>
    /// <param name="a">The triple.</param>
    /// <returns>The negation.</returns>
    public static Int3 operator -(Int3 a) => Negate(a);

    /// <summary>Scales a triple.</summary>
    /// <param name="a">The triple.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>Each component times <paramref name="factor"/>.</returns>
    public static Int3 operator *(Int3 a, int factor) => Multiply(a, factor);

    /// <summary>Add per component.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The sum.</returns>
    public static Int3 Add(Int3 a, Int3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Subtract per component.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The difference.</returns>
    public static Int3 Subtract(Int3 a, Int3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Negate per component.</summary>
    /// <param name="a">The operand.</param>
    /// <returns>The negation.</returns>
    public static Int3 Negate(Int3 a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Multiply per component.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>Each component times the factor.</returns>
    public static Int3 Multiply(Int3 a, int factor) => new(a.X * factor, a.Y * factor, a.Z * factor);

    /// <summary>Formats as <c>(x, y, z)</c>, which plan fingerprints depend on.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({this.X}, {this.Y}, {this.Z})");
}
