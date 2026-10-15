using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class Utf8ViewBase : ViewBase<Utf8ViewModel> { }

public sealed partial class Utf8View : Utf8ViewBase
{
    public Utf8View()
    {
        this.InitializeComponent();
    }
}
