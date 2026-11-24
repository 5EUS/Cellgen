namespace Cellgen.Generation;

/// <summary>Whether a cell's ground can sit on a level's <see cref="ElevationField"/>, and how far it must be lifted to.</summary>
internal static class ElevationFit
{
    /// <summary>Finds the lift, in steps, that puts every corner of a placed cell on the field.</summary>
    /// <param name="piece">The cell and where it would stand.</param>
    /// <param name="field">The level's field; null when the level has none, which accepts only cells whose ground is level.</param>
    /// <param name="lift">The lift (0 without a field).</param>
    /// <returns>True when all of the cell's corners agree on one lift.</returns>
    public static bool TryLift(PlacedPiece piece, ElevationField? field, out int lift)
    {
        int[] corners = piece.Variant.Corners;
        int cols = piece.Variant.Footprint.X + 1;
        int rows = piece.Variant.Footprint.Z + 1;
        lift = 0;
        bool first = true;
        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < cols; x++)
            {
                int ground = corners[(z * cols) + x];
                int wanted = 0;
                if (field is not null)
                {
                    int fx = piece.Anchor.X + x;
                    int fz = piece.Anchor.Z + z;
                    if (!field.Contains(fx, fz))
                    {
                        return false;
                    }

                    wanted = field.Get(fx, fz);
                }

                int needed = wanted - ground;
                if (first)
                {
                    lift = needed;
                    first = false;
                }
                else if (needed != lift)
                {
                    lift = 0;
                    return false;
                }
            }
        }

        if (field is null)
        {
            lift = 0;
        }

        return true;
    }
}
