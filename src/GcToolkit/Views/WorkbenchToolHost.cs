using System.Runtime.CompilerServices;
using GcToolkit.Core.Services;
using GcToolkit.Core.Workbench;
using GcToolkit.Services.Navigation;
using Microsoft.UI.Xaml.Media.Animation;

namespace GcToolkit.Views;

/// <summary>
/// Shows one workbench tool tab. Each tab gets its own <see cref="Frame"/> holding the tool's view
/// (dedicated or the shared <see cref="ToolHostView"/>), created once and reused, so a tool keeps its
/// state when the user switches tool tabs, caches or window sizes. Once the tool's view model exists it is
/// handed to the tab, which restores and tracks its saved inputs.
/// </summary>
public sealed partial class WorkbenchToolHost : ContentControl
{
    public static readonly DependencyProperty ToolProperty = DependencyProperty.Register(
        nameof(Tool),
        typeof(WorkbenchToolTab),
        typeof(WorkbenchToolHost),
        new PropertyMetadata(null, (d, _) => ((WorkbenchToolHost)d).Attach()));

    // Per window: a frame (like any element) belongs to one XamlRoot, and a popped-out tool gets its own.
    private static readonly ConditionalWeakTable<WorkbenchToolTab, Dictionary<XamlRoot, Frame>> _frames = new();

    private WorkbenchToolTab? _watched;

    public WorkbenchToolHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Attach();

        // A frame can only have one parent; let go of it so the next host showing this tab can take it.
        Unloaded += (_, _) => Release();
    }

    /// <summary>Drops the frame and stops following the tab.</summary>
    public void Release()
    {
        Watch(null);
        Content = null;
    }

    // When a tool comes back from its own window, this window's view model takes over its inputs again.
    private void Watch(WorkbenchToolTab? tool)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnToolPropertyChanged;
        }

        _watched = tool;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnToolPropertyChanged;
        }
    }

    private void OnToolPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkbenchToolTab.IsPoppedOut)
            && _watched is { IsPoppedOut: false } tool
            && Content is Frame { Content: IViewBase { ViewModel: { } viewModel } })
        {
            tool.Attach(viewModel);
        }
    }

    public WorkbenchToolTab? Tool
    {
        get => (WorkbenchToolTab?)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    private void Attach()
    {
        if (!IsLoaded || Tool is not { } tool || FindShell() is not { } shell || XamlRoot is not { } root)
        {
            return;
        }

        Watch(tool);
        var frames = _frames.GetOrCreateValue(tool);
        if (!frames.TryGetValue(root, out var frame))
        {
            frame = new Frame();
            var navigation = (NavigationService)shell.ServiceProvider.GetRequiredService<INavigationService>();
            var viewType = navigation.GetViewType(tool.ViewModelType) ?? typeof(ToolHostView);
            frame.Navigate(viewType, tool.ViewModelType, new SuppressNavigationTransitionInfo());
            frames[root] = frame;
            AttachWhenCreated(tool, frame);
        }

        if (!ReferenceEquals(Content, frame))
        {
            if (frame.Parent is WorkbenchToolHost previousHost)
            {
                previousHost.Content = null;
            }

            Content = frame;
        }
    }

    // The page resolves its view model while loading, after its own Loading handler has run.
    private static void AttachWhenCreated(WorkbenchToolTab tool, Frame frame)
    {
        if (frame.Content is not FrameworkElement page)
        {
            return;
        }

        void OnLoading(FrameworkElement sender, object args)
        {
            if (page is IViewBase { ViewModel: { } viewModel })
            {
                page.Loading -= OnLoading;
                tool.Attach(viewModel);
            }
        }

        page.Loading += OnLoading;
    }

    private WindowShell? FindShell() => XamlRoot?.Content switch
    {
        WindowShell shell => shell,
        ContentControl { Content: WindowShell shell } => shell,
        _ => null,
    };
}
