using System.Reflection;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;
using Microsoft.UI.Xaml.Media.Animation;

#if HAS_UNO
using Windows.UI.Core;
#endif

namespace GcToolkit.Services.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IWindowShellProvider _shellProvider;
    private readonly Dictionary<Type, Type> _viewModelToViewMap = new();
    private bool _initialized;
    private bool _backRequestedSubscribed;

    public NavigationService(IWindowShellProvider shellProvider)
    {
        _shellProvider = shellProvider;
    }

    private Frame Frame => _shellProvider.RootFrame;

    public bool CanGoBack => _initialized && Frame.CanGoBack;

    public NavigationSection? CurrentSection { get; private set; }

    public void Initialize()
    {
        _initialized = true;
#if HAS_UNO
		Frame.Navigated += OnFrameNavigated;
		UpdateBackRequestedSubscription();
#endif
    }

    public void RegisterView(Type viewType, Type viewModelType)
        => _viewModelToViewMap[viewModelType] = viewType;

    /// <summary>The view registered for <paramref name="viewModelType"/>, for hosting it outside the root frame.</summary>
    public Type? GetViewType(Type viewModelType) => _viewModelToViewMap.GetValueOrDefault(viewModelType);

    public void Navigate<TViewModel>() =>Navigate(typeof(TViewModel), null);

    public void Navigate<TViewModel>(object? parameter) => Navigate(typeof(TViewModel), parameter);

    public void Navigate(Type viewModelType, object? parameter = null)
        // The shared tool host needs the concrete ViewModel type to resolve; pass it as the frame
        // parameter when no explicit one is supplied (research R10). Routing the generic overloads
        // through here keeps that defaulting consistent so tool navigation never hands the host a null.
        => NavigateCore(viewModelType, parameter ?? viewModelType);

    private void NavigateCore(Type viewModelType, object? frameParameter)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("NavigationService not initialized. Call Initialize() first.");
        }

        if (!_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            throw new InvalidOperationException($"No view registered for ViewModel {viewModelType.Name}.");
        }

        var navInfo = GetNavigationInfo(viewType);
        if (navInfo is not null)
        {
            CurrentSection = navInfo.Section;
        }

        var transitionInfo = GetTransitionInfo(navInfo?.Transition ?? NavigationTransition.Default, isForward: true);
        Frame.Navigate(viewType, frameParameter, transitionInfo);

#if HAS_UNO
		UpdateBackRequestedSubscription();
#endif
    }

    public bool GoBack()
    {
        if (!CanGoBack)
        {
            return false;
        }

        var backEntry = Frame.BackStack.LastOrDefault();
        var transition = NavigationTransition.Default;

        if (backEntry is not null)
        {
            var navInfo = GetNavigationInfo(backEntry.SourcePageType);
            transition = navInfo?.Transition ?? NavigationTransition.Default;
            if (navInfo is not null)
            {
                CurrentSection = navInfo.Section;
            }
        }

        Frame.GoBack(GetTransitionInfo(transition, isForward: false));

#if HAS_UNO
		UpdateBackRequestedSubscription();
#endif

        return true;
    }

    public void ClearBackStack()
    {
        Frame.BackStack.Clear();
#if HAS_UNO
		UpdateBackRequestedSubscription();
#endif
    }

    private static NavigationInfoAttribute? GetNavigationInfo(Type viewType)
    {
        var attr = viewType.GetCustomAttribute<NavigationInfoAttribute>();
        if (attr is not null)
        {
            return attr;
        }

        // Walk the inheritance chain — needed because the sealed view class
        // may inherit from an intermediate ViewBase<T> that carries the attribute.
        var baseType = viewType.BaseType;
        while (baseType is not null && baseType != typeof(Page))
        {
            attr = baseType.GetCustomAttribute<NavigationInfoAttribute>();
            if (attr is not null)
            {
                return attr;
            }

            baseType = baseType.BaseType;
        }

        return null;
    }

    private static NavigationTransitionInfo GetTransitionInfo(NavigationTransition transition, bool isForward)
    {
        return transition switch
        {
            NavigationTransition.DrillIn => new DrillInNavigationTransitionInfo(),
            NavigationTransition.Entrance => new EntranceNavigationTransitionInfo(),
            NavigationTransition.Suppress => new SuppressNavigationTransitionInfo(),
            _ => new SlideNavigationTransitionInfo
            {
                Effect = isForward
                    ? SlideNavigationTransitionEffect.FromRight
                    : SlideNavigationTransitionEffect.FromLeft,
            },
        };
    }

#if HAS_UNO
	private void OnFrameNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
	{
		UpdateBackRequestedSubscription();
	}

	/// <summary>
	/// Manages BackRequested subscription. On Android 16+, subscribing/unsubscribing
	/// (not just Handled) controls whether the app or system handles the back gesture.
	/// </summary>
	private void UpdateBackRequestedSubscription()
	{
		var manager = SystemNavigationManager.GetForCurrentView();
		if (Frame.CanGoBack && !_backRequestedSubscribed)
		{
			manager.BackRequested += OnBackRequested;
			_backRequestedSubscribed = true;
		}
		else if (!Frame.CanGoBack && _backRequestedSubscribed)
		{
			manager.BackRequested -= OnBackRequested;
			_backRequestedSubscribed = false;
		}
	}

	private void OnBackRequested(object? sender, BackRequestedEventArgs e)
	{
		if (GoBack())
		{
			e.Handled = true;
		}
	}
#endif
}
