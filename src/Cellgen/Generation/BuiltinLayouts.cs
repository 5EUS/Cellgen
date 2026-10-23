namespace Cellgen.Generation;

/// <summary>The ids of the layouts every <see cref="LayoutRegistry"/> starts with.</summary>
public static class BuiltinLayouts
{
    /// <summary>Gets the id of the winding start-to-exit corridor with side branches.</summary>
    public const string Corridors = "corridors";

    /// <summary>Gets the id of the whole-grid wilderness, whose clusters come from sockets alone.</summary>
    public const string Wilds = "wilds";
}
