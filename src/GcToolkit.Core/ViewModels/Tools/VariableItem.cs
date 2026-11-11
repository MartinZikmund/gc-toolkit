namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One variable in the calculator's grid. Dimmed in the UI while it has no value.</summary>
public sealed partial class VariableItem(string name, string value, Action<VariableItem> changed) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial string Value { get; set; } = value;

    public bool IsMissing => string.IsNullOrWhiteSpace(Value);

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(IsMissing));
        changed(this);
    }
}
