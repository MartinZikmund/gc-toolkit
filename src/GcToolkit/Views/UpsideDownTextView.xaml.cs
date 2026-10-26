using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class UpsideDownTextViewBase : ViewBase<UpsideDownTextViewModel> { }

public sealed partial class UpsideDownTextView : UpsideDownTextViewBase
{
    public UpsideDownTextView()
    {
        this.InitializeComponent();
    }
}
