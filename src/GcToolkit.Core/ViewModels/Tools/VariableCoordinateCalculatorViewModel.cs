using System.Collections.ObjectModel;
using System.Globalization;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Workbench;
using Microsoft.Extensions.Localization;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Variable coordinate calculator (issue #309). Type a coordinate template such as
/// <c>N 50° 0A.BCD E 014° 2E.FGH</c>; the variables it uses appear in a grid, and the final coordinate
/// is solved live in the chosen format. All the maths is in <see cref="CoordinateTemplateSolver"/>;
/// this view model only wires the sheet to bindings and remembers it between visits.
/// Several stages (templates) share one set of variables, like a multi-stage cache.
/// </summary>
[Tool("VariableCoordinateCalculator", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords = ["variable", "formula", "template", "mystery", "puzzle", "final", "letters", "proměnné", "vzorec", "souřadnice", "mystery", "záhada", "finálka"])]
public sealed partial class VariableCoordinateCalculatorViewModel : ToolViewModelBase
{
    private const string PreferencesKey = "variable-coordinate-sheet";
    private const string ExampleTemplate = "N 50° 0A.BCD E 014° 2E.FGH";
    private const string ExamplePosted = "N 50° 01.000 E 014° 25.000";
    private const string ExampleLetters = "ABCDEFGH";
    private const string ExampleDigits = "12345678";

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly IPreferences _preferences;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VariableItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private bool _suspended;

    public VariableCoordinateCalculatorViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share,
        IPreferences preferences)
        : base("VariableCoordinateCalculator", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        _preferences = preferences;

        Load();
    }

    /// <summary>The coordinate templates; there is always at least one.</summary>
    public ObservableCollection<CoordinateStageItem> Stages { get; } = [];

    /// <summary>The variables used by any stage, in order of first use.</summary>
    public ObservableCollection<VariableItem> Variables { get; } = [];

    /// <summary>The chosen output notation, as a <see cref="CoordinateFormat"/> value (default: degrees and decimal minutes).</summary>
    [ObservableProperty]
    public partial int FormatIndex { get; set; } = (int)CoordinateFormat.DegreesDecimalMinutes;

    /// <summary>The cache's posted coordinates (free text); optional, enables the distance check.</summary>
    [ObservableProperty]
    public partial string PostedCoordinate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPostedError { get; set; }

    /// <summary><see langword="true"/> when at least one stage is solved (gates Copy/Share).</summary>
    [ObservableProperty]
    public partial bool HasAnyResult { get; set; }

    partial void OnFormatIndexChanged(int value) => SheetChanged();

    partial void OnPostedCoordinateChanged(string value) => SheetChanged();

    partial void OnHasAnyResultChanged(bool value)
    {
        CopyAllCommand.NotifyCanExecuteChanged();
        ShareAllCommand.NotifyCanExecuteChanged();
    }

    private CoordinateFormat SelectedFormat => Enum.IsDefined((CoordinateFormat)FormatIndex)
        ? (CoordinateFormat)FormatIndex
        : CoordinateFormat.DegreesDecimalMinutes;

    private void SheetChanged()
    {
        if (_suspended)
        {
            return;
        }

        Recompute();
        Save();
    }

    private CoordinateStageItem NewStage(string name, string template) => new(name, template, SheetChanged, RemoveStage, CopyText);

    private void Recompute()
    {
        GeoCoordinate? posted = null;
        HasPostedError = false;
        if (!string.IsNullOrWhiteSpace(PostedCoordinate))
        {
            if (CoordinateParser.TryParse(PostedCoordinate, out var parsed, out _) && parsed.IsValid)
            {
                posted = parsed;
            }
            else
            {
                HasPostedError = true;
            }
        }

        var solutions = Stages.Select(s => CoordinateTemplateSolver.Solve(s.Template, _values, posted)).ToList();
        SyncVariables(solutions.SelectMany(s => s.Variables).Distinct().ToList());

        // Values may have been touched by the sync, so solve once more on the final set.
        solutions = [.. Stages.Select(s => CoordinateTemplateSolver.Solve(s.Template, _values, posted))];

        for (var i = 0; i < Stages.Count; i++)
        {
            Show(Stages[i], solutions[i]);
        }

        HasAnyResult = Stages.Any(s => s.IsComplete);
    }

    private void Show(CoordinateStageItem stage, TemplateSolution solution)
    {
        stage.CanRemove = Stages.Count > 1;
        stage.ResultText = solution.Coordinate is { } c ? CoordinateFormatter.Format(c, SelectedFormat) : string.Empty;

        var blank = string.IsNullOrWhiteSpace(stage.Template);
        if (solution.Error is { } error && !blank)
        {
            stage.ErrorPosition = error.Position;
            stage.ErrorText = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["VariableCoordinateErrorAt"].Value,
                error.Position + 1,
                _localizer[$"FormulaError_{error.Kind}"].Value);
        }
        else
        {
            stage.ErrorPosition = -1;
            stage.ErrorText = string.Empty;
        }

        stage.MissingText = !blank && solution.Error is null && solution.MissingVariables.Count > 0
            ? string.Format(
                CultureInfo.CurrentCulture,
                _localizer["VariableCoordinateWaitingFor"].Value,
                string.Join(", ", solution.MissingVariables))
            : string.Empty;

        stage.DistanceText = solution.DistanceMeters is { } meters ? FormatDistance(meters) : string.Empty;
        stage.IsWithinTwoMiles = solution.IsWithinTwoMiles;
    }

    private static string FormatDistance(double meters)
        => meters < 1000.0
            ? string.Create(CultureInfo.CurrentCulture, $"{meters:0} m")
            : string.Create(CultureInfo.CurrentCulture, $"{meters / 1000.0:0.00} km");

    /// <summary>Keeps one grid row per used variable, reusing rows (and typed values) that are still needed.</summary>
    private void SyncVariables(IReadOnlyList<string> names)
    {
        if (Variables.Select(v => v.Name).SequenceEqual(names, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        Variables.Clear();
        foreach (var name in names)
        {
            if (!_items.TryGetValue(name, out var item))
            {
                item = new VariableItem(name, _values.GetValueOrDefault(name, string.Empty), OnVariableValueChanged);
                _items[name] = item;
            }

            Variables.Add(item);
        }
    }

    private void OnVariableValueChanged(VariableItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Value))
        {
            _values.Remove(item.Name);
        }
        else
        {
            _values[item.Name] = item.Value.Trim();
        }

        SheetChanged();
    }

    private void Load()
    {
        SavedVariableSheet? saved = null;
        try
        {
            if (_preferences.TryGetComplex<SavedVariableSheet>(PreferencesKey, out var stored))
            {
                saved = stored;
            }
        }
        catch (Exception)
        {
            // Unreadable stored data is the same as no stored data.
        }

        _suspended = true;
        try
        {
            if (saved is not null)
            {
                foreach (var (name, value) in saved.Variables)
                {
                    _values[name.ToUpperInvariant()] = value;
                }

                foreach (var stage in saved.Stages)
                {
                    Stages.Add(NewStage(stage.Name, stage.Template));
                }

                PostedCoordinate = saved.Posted;
                FormatIndex = saved.FormatIndex;
            }

            EnsureAStage();
        }
        finally
        {
            _suspended = false;
        }

        Recompute();
    }

    private void Save()
    {
        SavedVariableSheet sheet = new(
            new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase),
            [.. Stages.Select(s => new SavedStage(s.Name, s.Template))],
            PostedCoordinate,
            FormatIndex);
        try
        {
            _preferences.SetComplex(PreferencesKey, sheet);
        }
        catch (Exception)
        {
            // Not being able to remember the sheet must never break the calculator.
        }
    }

    private void EnsureAStage()
    {
        if (Stages.Count == 0)
        {
            Stages.Add(NewStage(DefaultStageName(1), string.Empty));
        }
    }

    private string DefaultStageName(int number)
        => string.Format(CultureInfo.CurrentCulture, _localizer["VariableCoordinateStageName"].Value, number);

    private IEnumerable<string> FinishedResults() => Stages.Where(s => s.IsComplete).Select(s => s.ResultText);

    [RelayCommand]
    private void AddStage()
    {
        Stages.Add(NewStage(DefaultStageName(Stages.Count + 1), string.Empty));
        SheetChanged();
    }

    [RelayCommand]
    private void RemoveStage(CoordinateStageItem? stage)
    {
        if (stage is null || Stages.Count <= 1)
        {
            return;
        }

        Stages.Remove(stage);
        SheetChanged();
    }

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand(CanExecute = nameof(HasAnyResult))]
    private void CopyAll() => _clipboard.SetText(string.Join(Environment.NewLine, FinishedResults()));

    [RelayCommand(CanExecute = nameof(HasAnyResult))]
    private async Task ShareAllAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, string.Join(Environment.NewLine, FinishedResults()));
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void LoadExample()
    {
        _suspended = true;
        try
        {
            ResetSheet();
            Stages[0].Template = ExampleTemplate;
            for (var i = 0; i < ExampleLetters.Length; i++)
            {
                _values[ExampleLetters[i].ToString()] = ExampleDigits[i].ToString();
            }

            PostedCoordinate = ExamplePosted;
        }
        finally
        {
            _suspended = false;
        }

        SheetChanged();
    }

    [RelayCommand]
    private void Clear()
    {
        _suspended = true;
        try
        {
            ResetSheet();
        }
        finally
        {
            _suspended = false;
        }

        SheetChanged();
    }

    private void ResetSheet()
    {
        Stages.Clear();
        Variables.Clear();
        _items.Clear();
        _values.Clear();
        PostedCoordinate = string.Empty;
        EnsureAStage();
    }
}
