using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class RhumbLineViewBase : ViewBase<RhumbLineViewModel> { }

public sealed partial class RhumbLineView : RhumbLineViewBase
{
    public RhumbLineView()
    {
        this.InitializeComponent();
    }

    public static Visibility BothTrue(bool a, bool b) => a && b ? Visibility.Visible : Visibility.Collapsed;
}
