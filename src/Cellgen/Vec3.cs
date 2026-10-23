using System.Globalization;
using System.Runtime.InteropServices;

namespace Cellgen;

/// <summary>A float triple: a position or size in a cell's local space.</summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Vec3(float X, float Y, float Z)
{
    /// <summary>Gets <c>(0, 0, 0)</c>.</summary>
    public static Vec3 Zero => new(0f, 0f, 0f);

    /// <summary>Adds two vectors per component.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The sum.</returns>
    public static Vec3 operator +(Vec3 a, Vec3 b) => Add(a, b);

    /// <summary>Scales a vector.</summary>
    /// <param name="a">The vector.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>Each component times <paramref name="factor"/>.</returns>
    public static Vec3 operator *(Vec3 a, float factor) => Multiply(a, factor);

    /// <summary>Add per component.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>The sum.</returns>
    public static Vec3 Add(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Multiply per component.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>Each component times the factor.</returns>
    public static Vec3 Multiply(Vec3 a, float factor) => new(a.X * factor, a.Y * factor, a.Z * factor);

    /// <summary>Formats as <c>(x, y, z)</c>.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({this.X}, {this.Y}, {this.Z})");
}
