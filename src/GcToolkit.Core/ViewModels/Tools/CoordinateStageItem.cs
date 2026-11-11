using System.Windows.Input;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One coordinate template (a stage of the cache) with its live result.</summary>
public sealed partial class CoordinateStageItem(string name, string template, Action changed, Action<CoordinateStageItem> remove, Action<string> copy) : ObservableObject
{
    private ICommand? _removeCommand;
    private ICommand? _copyCommand;

    public ICommand RemoveCommand => _removeCommand ??= new RelayCommand(() => remove(this));

    public ICommand CopyCommand => _copyCommand ??= new RelayCommand(() => copy(ResultText));

    [ObservableProperty]
    public partial string Name { get; set; } = name;

    [ObservableProperty]
    public partial string Template { get; set; } = template;

    /// <summary>The solved coordinate in the chosen format; empty until the stage is complete.</summary>
    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    /// <summary>Zero-based character index of the error in the template, or -1.</summary>
    [ObservableProperty]
    public partial int ErrorPosition { get; set; } = -1;

    /// <summary>"Waiting for A, B" while variables have no value.</summary>
    [ObservableProperty]
    public partial string MissingText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsWithinTwoMiles { get; set; }

    public bool IsComplete => ResultText.Length > 0;

    public bool HasError => ErrorText.Length > 0;

    public bool HasMissing => MissingText.Length > 0;

    public bool HasDistance => DistanceText.Length > 0;

    /// <summary><see langword="false"/> for a lonely stage, so the UI does not offer to delete the only template.</summary>
    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    partial void OnTemplateChanged(string value) => changed();

    partial void OnNameChanged(string value) => changed();

    partial void OnResultTextChanged(string value) => OnPropertyChanged(nameof(IsComplete));

    partial void OnErrorTextChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnMissingTextChanged(string value) => OnPropertyChanged(nameof(HasMissing));

    partial void OnDistanceTextChanged(string value) => OnPropertyChanged(nameof(HasDistance));
}
