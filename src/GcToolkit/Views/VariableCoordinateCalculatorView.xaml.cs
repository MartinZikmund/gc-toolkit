using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class VariableCoordinateCalculatorViewBase : ViewBase<VariableCoordinateCalculatorViewModel> { }

public sealed partial class VariableCoordinateCalculatorView : VariableCoordinateCalculatorViewBase
{
    public VariableCoordinateCalculatorView()
    {
        this.InitializeComponent();
    }

    public static double DimIfMissing(bool isMissing) => isMissing ? 0.6 : 1.0;

    public static Visibility ShowWhenEmpty(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ShowWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
