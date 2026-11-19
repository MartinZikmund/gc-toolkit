using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Controls;

/// <summary>A variable sheet's stages and variables. Used by the variable coordinate calculator and the workbench.</summary>
public sealed partial class VariableSheetPanel : UserControl
{
    public VariableSheetPanel()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(VariableCoordinateCalculatorViewModel),
        typeof(VariableSheetPanel),
        new PropertyMetadata(null, (d, _) => ((VariableSheetPanel)d).Bindings.Update()));

    public VariableCoordinateCalculatorViewModel? ViewModel
    {
        get => (VariableCoordinateCalculatorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static double DimIfMissing(bool isMissing) => isMissing ? 0.6 : 1.0;

    public static Visibility ShowWhenEmpty(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ShowWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
