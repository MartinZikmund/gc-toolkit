#if __ANDROID__
using Android.App;
using Android.Runtime;
using AndroidX.Core.Content;
using AndroidX.Window.Java.Layout;
using AndroidX.Window.Layout;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Views;

/// <summary>Finds where a foldable's hinge crosses an element and reports it whenever the fold changes (#466).</summary>
/// <remarks>
/// Uses Jetpack WindowManager directly: Uno's Foldable add-in subscribes to the activity lifecycle only when
/// first asked, after the activity started, so it never sees a fold (and raises nothing when one appears).
/// </remarks>
internal sealed class HingeWatcher(FrameworkElement element, Action<WorkbenchSpan?> changed) : Java.Lang.Object, AndroidX.Core.Util.IConsumer
{
    private WindowInfoTrackerCallbackAdapter? _tracker;
    private Android.Graphics.Rect? _fold;

    public void Start()
    {
        if (_tracker is null && Uno.UI.ContextHelper.Current is Activity activity)
        {
            _tracker = new(WindowInfoTracker.Companion.GetOrCreate(activity));
            _tracker.AddWindowLayoutInfoListener(activity, ContextCompat.GetMainExecutor(activity), this);
        }
    }

    public void Stop()
    {
        _tracker?.RemoveWindowLayoutInfoListener(this);
        _tracker = null;
    }

    public void Accept(Java.Lang.Object? value)
    {
        // Only a vertical fold that splits the window gets two panes; a flat or horizontal one keeps the normal layout.
        _fold = value?.JavaCast<WindowLayoutInfo>().DisplayFeatures
            .Select(AsFold)
            .FirstOrDefault(fold => fold is { IsSeparating: true } && fold.Orientation == FoldingFeatureOrientation.Vertical)
            ?.Bounds;
        Refresh();
    }

    public void Refresh() => changed(Find());

    // Java interfaces need a JavaCast; a plain C# cast misses them.
    private static IFoldingFeature? AsFold(IDisplayFeature feature)
    {
        try
        {
            return ((Java.Lang.Object)feature).JavaCast<IFoldingFeature>();
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private WorkbenchSpan? Find()
    {
        if (_fold is not { } fold || element.XamlRoot is not { } root)
        {
            return null;
        }

        // The fold is in physical pixels of the window.
        var scale = root.RasterizationScale;
        var hingeStart = fold.Left / scale - element.TransformToVisual(null).TransformPoint(default).X;
        var hingeWidth = fold.Width() / scale;
        return hingeStart > 0 && hingeStart + hingeWidth < element.ActualWidth
            ? new WorkbenchSpan(hingeStart, hingeWidth)
            : null;
    }
}
#endif
