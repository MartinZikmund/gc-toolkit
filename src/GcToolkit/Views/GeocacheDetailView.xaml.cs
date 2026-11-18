using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Caches)]
public partial class GeocacheDetailViewBase : ViewBase<GeocacheDetailViewModel> { }

public sealed partial class GeocacheDetailView : GeocacheDetailViewBase
{
    public GeocacheDetailView() => this.InitializeComponent();
}
