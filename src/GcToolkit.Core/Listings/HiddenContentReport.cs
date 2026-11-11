namespace GcToolkit.Core.Listings;

/// <summary>The result of inspecting a listing.</summary>
/// <param name="Findings">Everything suspicious, in document order.</param>
/// <param name="VisibleText">The text a reader would actually see, with hidden text removed.</param>
public sealed record HiddenContentReport(IReadOnlyList<HiddenContentFinding> Findings, string VisibleText);
