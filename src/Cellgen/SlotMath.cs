namespace Cellgen;

/// <summary>
/// <para>
/// Slot-grid geometry: faces, quarter turns, and where a cell's slots and
/// sockets land once it is rotated.
/// </para>
/// <para>
/// A cell is authored with its slot <c>(0,0,0)</c> at the scene origin and
/// its footprint extending along <c>+X</c>, <c>+Y</c> and <c>+Z</c>. A
/// rotation of <c>r</c> quarter turns is <c>Basis.FromEuler(0, r·90°, 0)</c>
/// about the footprint's centre, which maps East to North: every function
/// here agrees with that one transform, and <c>tools/procgen_check.gd</c>
/// asserts it.
/// </para>
/// </summary>
public static class SlotMath
{
    /// <summary>The six faces, horizontals first.</summary>
    public static readonly CellFace[] Faces =
        [CellFace.North, CellFace.East, CellFace.South, CellFace.West, CellFace.Up, CellFace.Down];

    /// <summary>Gets the slot-space step across a face.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The unit offset to the neighbouring slot.</returns>
    public static Int3 Offset(CellFace face) => face switch
    {
        CellFace.North => new Int3(0, 0, -1),
        CellFace.East => new Int3(1, 0, 0),
        CellFace.South => new Int3(0, 0, 1),
        CellFace.West => new Int3(-1, 0, 0),
        CellFace.Up => new Int3(0, 1, 0),
        CellFace.Down => new Int3(0, -1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    /// <summary>Gets the face on the other side of a shared wall.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The opposite face.</returns>
    public static CellFace Opposite(CellFace face) => face switch
    {
        CellFace.North => CellFace.South,
        CellFace.East => CellFace.West,
        CellFace.South => CellFace.North,
        CellFace.West => CellFace.East,
        CellFace.Up => CellFace.Down,
        CellFace.Down => CellFace.Up,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
    };

    /// <summary>Gets whether a face is horizontal (not Up or Down).</summary>
    /// <param name="face">The face.</param>
    /// <returns>True for North, East, South and West.</returns>
    public static bool IsHorizontal(CellFace face) => face is not (CellFace.Up or CellFace.Down);

    /// <summary>
    /// Rotates a face by <paramref name="quarterTurns"/>. Up and Down are
    /// unaffected; East goes to North for one turn.
    /// </summary>
    /// <param name="face">The face.</param>
    /// <param name="quarterTurns">Quarter turns, any integer.</param>
    /// <returns>The rotated face.</returns>
    public static CellFace Rotate(CellFace face, int quarterTurns)
    {
        if (!IsHorizontal(face))
        {
            return face;
        }

        return (CellFace)Mod((int)face - quarterTurns, 4);
    }

    /// <summary>Gets a footprint's size after rotation.</summary>
    /// <param name="footprint">The authored footprint.</param>
    /// <param name="quarterTurns">Quarter turns.</param>
    /// <returns>The rotated footprint; X and Z swap on odd turns.</returns>
    public static Int3 RotateFootprint(Int3 footprint, int quarterTurns) =>
        (Mod(quarterTurns, 2) == 0) ? footprint : Swapped(footprint);

    /// <summary>
    /// Maps a slot inside an authored footprint to its slot inside the
    /// rotated footprint.
    /// </summary>
    /// <param name="slot">The slot, in authored footprint space.</param>
    /// <param name="footprint">The authored footprint.</param>
    /// <param name="quarterTurns">Quarter turns.</param>
    /// <returns>The slot in rotated footprint space.</returns>
    public static Int3 RotateSlot(Int3 slot, Int3 footprint, int quarterTurns)
    {
        Int3 result = slot;
        Int3 size = footprint;
        for (int i = 0; i < Mod(quarterTurns, 4); i++)
        {
            // One turn: (x, z) -> (z, fx - 1 - x), footprint (fx, fz) -> (fz, fx).
            result = new Int3(result.Z, result.Y, size.X - 1 - result.X);
            size = Swapped(size);
        }

        return result;
    }

    /// <summary>Rotates a grid of slot-corner values (row-major, <c>(footprint.X + 1) * (footprint.Z + 1)</c>) by quarter turns, the way <see cref="RotateSlot"/> turns a slot.</summary>
    /// <param name="values">The corner values of the unrotated footprint.</param>
    /// <param name="footprint">The unrotated footprint.</param>
    /// <param name="quarterTurns">Clockwise quarter turns, as everywhere else.</param>
    /// <returns>The values for the rotated footprint, row-major over its own corners.</returns>
    public static int[] RotateCorners(IReadOnlyList<int> values, Int3 footprint, int quarterTurns)
    {
        int cols = footprint.X + 1;
        int rows = footprint.Z + 1;
        var current = new int[cols * rows];
        for (int i = 0; i < current.Length; i++)
        {
            current[i] = i < values.Count ? values[i] : 0;
        }

        int fx = footprint.X;
        int fz = footprint.Z;
        for (int turn = 0; turn < Mod(quarterTurns, 4); turn++)
        {
            // One turn: corner (x, z) -> (z, fx - x), footprint (fx, fz) -> (fz, fx).
            var next = new int[current.Length];
            for (int z = 0; z <= fz; z++)
            {
                for (int x = 0; x <= fx; x++)
                {
                    next[((fx - x) * (fz + 1)) + z] = current[(z * (fx + 1)) + x];
                }
            }

            current = next;
            (fx, fz) = (fz, fx);
        }

        return current;
    }

    /// <summary>Gets whether a slot lies within a footprint.</summary>
    /// <param name="footprint">The footprint.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>True when every coordinate is in range.</returns>
    public static bool Contains(Int3 footprint, Int3 slot) =>
        slot.X >= 0 && slot.Y >= 0 && slot.Z >= 0 && slot.X < footprint.X && slot.Y < footprint.Y && slot.Z < footprint.Z;

    /// <summary>Gets a non-negative modulus.</summary>
    /// <param name="value">The value.</param>
    /// <param name="modulus">The modulus, positive.</param>
    /// <returns><paramref name="value"/> mod <paramref name="modulus"/>, in <c>[0, modulus)</c>.</returns>
    public static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    private static Int3 Swapped(Int3 value)
    {
        (int across, int height, int along) = value;
        return new Int3(along, height, across);
    }
}
