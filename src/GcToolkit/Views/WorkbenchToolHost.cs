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

    private static readonly ConditionalWeakTable<WorkbenchToolTab, Frame> _frames = new();

    public WorkbenchToolHost()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += (_, _) => Attach();

        // A frame can only have one parent; let go of it so the next host showing this tab can take it.
        Unloaded += (_, _) => Content = null;
    }

    public WorkbenchToolTab? Tool
    {
        get => (WorkbenchToolTab?)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    private void Attach()
    {
        if (!IsLoaded || Tool is not { } tool || FindShell() is not { } shell)
        {
            return;
        }

        if (!_frames.TryGetValue(tool, out var frame))
        {
            frame = new Frame();
            var navigation = (NavigationService)shell.ServiceProvider.GetRequiredService<INavigationService>();
            var viewType = navigation.GetViewType(tool.ViewModelType) ?? typeof(ToolHostView);
            frame.Navigate(viewType, tool.ViewModelType, new SuppressNavigationTransitionInfo());
            _frames.Add(tool, frame);
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
