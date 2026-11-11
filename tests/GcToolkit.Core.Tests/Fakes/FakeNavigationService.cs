using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>No-op <see cref="INavigationService"/> for unit-testing ViewModels that navigate.</summary>
public sealed class FakeNavigationService : INavigationService
{
    public bool CanGoBack => false;

    public NavigationSection? CurrentSection => null;

    public void Initialize()
    {
    }

    public Type? LastViewModelType { get; private set; }

    public object? LastParameter { get; private set; }

    public void Navigate<TViewModel>() => Navigate(typeof(TViewModel));

    public void Navigate<TViewModel>(object? parameter) => Navigate(typeof(TViewModel), parameter);

    public void Navigate(Type viewModelType, object? parameter = null)
    {
        LastViewModelType = viewModelType;
        LastParameter = parameter;
    }

    public bool GoBack() => false;

    public void ClearBackStack()
    {
    }
}
