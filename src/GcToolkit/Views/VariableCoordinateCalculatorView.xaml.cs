using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class VariableCoordinateCalculatorViewBase : ViewBase<VariableCoordinateCalculatorViewModel> { }

public sealed partial class VariableCoordinateCalculatorView : VariableCoordinateCalculatorViewBase
{
    public VariableCoordinateCalculatorView()
    {
        this.InitializeComponent();
    }
}
