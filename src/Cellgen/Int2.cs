using System.Globalization;
using System.Runtime.InteropServices;

namespace Cellgen;

/// <summary>An integer pair: a horizontal grid size or a count of columns.</summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component (Z in the world).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Int2(int X, int Y)
{
    /// <summary>Formats as <c>(x, y)</c>.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({this.X}, {this.Y})");
}
