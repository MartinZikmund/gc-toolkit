using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace GcToolkit.Controls;

/// <summary>
/// A drag handle between two panes, reporting how far it moved (mouse, touch, pen or arrow keys).
/// Stands in for the Community Toolkit sizers, whose current builds do not load on Uno 7 heads.
/// </summary>
public sealed partial class PaneResizer : Control
{
    private const double KeyboardStep = 16;

    private Point? _lastPosition;

    public PaneResizer()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        ManipulationMode = ManipulationModes.None;
        Loaded += (_, _) => ProtectedCursor = InputSystemCursor.Create(
            IsHorizontalResize ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
    }

    /// <summary><see cref="Orientation.Vertical"/> is a vertical bar that resizes sideways.</summary>
    public Orientation Orientation { get; set; } = Orientation.Vertical;

    /// <summary>Raised with the change along the resize axis, in DIPs.</summary>
    public event EventHandler<double>? Resized;

    private bool IsHorizontalResize => Orientation == Orientation.Vertical;

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (CapturePointer(e.Pointer))
        {
            // Window coordinates: the handle itself moves while it is dragged.
            _lastPosition = e.GetCurrentPoint(null).Position;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPosition is not { } last)
        {
            return;
        }

        var position = e.GetCurrentPoint(null).Position;
        var delta = IsHorizontalResize ? position.X - last.X : position.Y - last.Y;
        _lastPosition = position;
        if (delta != 0)
        {
            Resized?.Invoke(this, delta);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPosition = null;
        ReleasePointerCapture(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _lastPosition = null;
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        double? step = (e.Key, IsHorizontalResize) switch
        {
            (VirtualKey.Left, true) or (VirtualKey.Up, false) => -KeyboardStep,
            (VirtualKey.Right, true) or (VirtualKey.Down, false) => KeyboardStep,
            _ => null,
        };

        if (step is { } delta)
        {
            Resized?.Invoke(this, delta);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
