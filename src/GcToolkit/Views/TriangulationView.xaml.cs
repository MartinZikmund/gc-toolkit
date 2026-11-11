using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class TriangulationViewBase : ViewBase<TriangulationViewModel> { }

public sealed partial class TriangulationView : TriangulationViewBase
{
    public TriangulationView()
    {
        this.InitializeComponent();
    }
}
