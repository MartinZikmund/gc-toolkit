using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ResectionViewBase : ViewBase<ResectionViewModel> { }

public sealed partial class ResectionView : ResectionViewBase
{
    public ResectionView() => InitializeComponent();
}
