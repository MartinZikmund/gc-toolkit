using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Messages;

/// <summary>Sent whenever the open caches or the active one change, so the tab strip and Home stay in sync.</summary>
public sealed record WorkbenchSessionChangedMessage(IReadOnlyList<GeocacheCode> OpenCaches, GeocacheCode? ActiveCache);
