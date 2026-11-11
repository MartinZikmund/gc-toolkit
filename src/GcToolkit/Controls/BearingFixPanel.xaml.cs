using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Controls;

/// <summary>The shared form, results and sketch of the bearing tools; each tool view supplies its own wording.</summary>
public sealed partial class BearingFixPanel : UserControl
{
    public BearingFixPanel() => InitializeComponent();

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(BearingFixViewModelBase), typeof(BearingFixPanel), new PropertyMetadata(null));

    public BearingFixViewModelBase? ViewModel
    {
        get => (BearingFixViewModelBase?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty PointsHeaderProperty = DependencyProperty.Register(
        nameof(PointsHeader), typeof(string), typeof(BearingFixPanel), new PropertyMetadata(string.Empty));

    public string PointsHeader
    {
        get => (string)GetValue(PointsHeaderProperty);
        set => SetValue(PointsHeaderProperty, value);
    }

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(BearingFixPanel), new PropertyMetadata(string.Empty));

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public static readonly DependencyProperty AddRowTextProperty = DependencyProperty.Register(
        nameof(AddRowText), typeof(string), typeof(BearingFixPanel), new PropertyMetadata(string.Empty));

    public string AddRowText
    {
        get => (string)GetValue(AddRowTextProperty);
        set => SetValue(AddRowTextProperty, value);
    }

    public static readonly DependencyProperty ResultHeaderProperty = DependencyProperty.Register(
        nameof(ResultHeader), typeof(string), typeof(BearingFixPanel), new PropertyMetadata(string.Empty));

    public string ResultHeader
    {
        get => (string)GetValue(ResultHeaderProperty);
        set => SetValue(ResultHeaderProperty, value);
    }
}
