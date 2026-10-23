namespace Cellgen.Generation;

/// <summary>One open face of a placed cell.</summary>
/// <param name="Slot">The slot within the cell's rotated footprint.</param>
/// <param name="Face">Which face of that slot, as placed.</param>
/// <param name="Socket">What the face connects to.</param>
public readonly record struct OpenSocket(Int3 Slot, CellFace Face, SocketData Socket);
