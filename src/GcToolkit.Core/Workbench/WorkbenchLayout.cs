namespace GcToolkit.Core.Workbench;

public enum WorkbenchLayoutMode
{
    /// <summary>Phone: one section at a time, switched with a selector bar.</summary>
    Compact,

    /// <summary>Listing full width; the tool pane slides in over it.</summary>
    Medium,

    /// <summary>Listing and tool pane side by side with a draggable divider.</summary>
    Wide,
}

/// <summary>What a phone-sized workbench shows.</summary>
public enum WorkbenchPhoneSection
{
    Listing,
    Tool,
    Scratchpad,
}

/// <summary>What the panel docked under the tools shows.</summary>
public enum WorkbenchDockSection
{
    Variables,
    Scratchpad,
}

/// <summary>Size rules of the workbench, measured on the workbench's own width (not the window's).</summary>
public static class WorkbenchLayout
{
    public const double MediumMinWidth = 640;
    public const double WideMinWidth = 840;
    public const double DefaultToolPaneWidth = 400;
    public const double MinToolPaneWidth = 320;
    public const double MinListingWidth = 400;
    public const double OverlayPaneWidth = 380;
    public const double DefaultDockHeight = 240;
    public const double MinDockHeight = 120;

    public static WorkbenchLayoutMode ModeFor(double width) => width switch
    {
        >= WideMinWidth => WorkbenchLayoutMode.Wide,
        >= MediumMinWidth => WorkbenchLayoutMode.Medium,
        _ => WorkbenchLayoutMode.Compact,
    };

    /// <summary>Keeps the tool pane usable and leaves the listing at least <see cref="MinListingWidth"/>.</summary>
    public static double ClampToolPaneWidth(double requested, double availableWidth)
    {
        var max = availableWidth > 0
            ? Math.Max(MinToolPaneWidth, availableWidth - MinListingWidth)
            : double.MaxValue;
        return Math.Clamp(requested, MinToolPaneWidth, max);
    }
}
