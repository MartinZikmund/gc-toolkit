using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PolygonAreaViewBase : ViewBase<PolygonAreaViewModel> { }

public sealed partial class PolygonAreaView : PolygonAreaViewBase
{
    public PolygonAreaView()
    {
        this.InitializeComponent();
    }
}
