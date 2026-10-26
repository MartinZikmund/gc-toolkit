using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class IsbnViewBase : ViewBase<IsbnViewModel> { }

public sealed partial class IsbnView : IsbnViewBase
{
    public IsbnView()
    {
        this.InitializeComponent();
    }
}
