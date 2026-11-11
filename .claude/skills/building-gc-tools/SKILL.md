---
name: building-gc-tools
description: >-
  Use when adding a new tool to the GC Toolbox / GcToolkit tool gallery — a converter, cipher,
  calculator, encoder, or any geocaching utility. Trigger when the user says "add a tool", "new
  tool", "build a tool", "add X to the gallery", "implement the <something> tool", references a
  tool issue, or asks to create a [Tool]-decorated ViewModel, its View, the localized strings, a
  tool icon, or a new tool category. Covers the attribute-based discovery system: where each file
  goes, the Core/head split, crossing the boundary with services, stubs vs real tools, icons,
  feature parity, and how to build/verify all heads.
---

# Building tools for GC Toolbox

A **tool** is one entry in the gallery (Morse, Roman numerals, Caesar, …). Tools are discovered at
**compile time** by a Roslyn source generator (`GcToolkit.SourceGenerators`) — so adding one needs
**no edit to any shared/central file**: no DI line, no `App.xaml.cs`, no `WindowShell`, no nav table.
You add a ViewModel, two strings, an icon, and (for real UI) a View, then rebuild.

The general C#/XAML conventions in `AGENTS.md` and `.claude/rules/` still apply — read those first if
you haven't. This skill is the tool-authoring workflow specifically.

## How discovery works (30 seconds)

- **All ViewModels live in `GcToolkit.Core`. All Views live in the `GcToolkit` app head.**
- The generator scans Core for `[Tool]`-decorated ViewModels, scans the head for `ViewBase<TVm>`
  views, validates the `.resw` strings, and emits the catalog, the navigation tree, and **all** view
  registrations.
- A tool with a matching `ViewBase<TVm>` view is routed to that view and marked **not a placeholder**.
  A tool with **no** view still appears in the gallery but opens the shared `ToolHostView` with a
  "placeholder" notice — handy for stubbing a tool before its UI exists.

## The shape of a tool

Every tool is the same spine. Confirmed identical across Morse, Roman numerals, and Caesar:

| Piece | Location | Required? |
|---|---|---|
| Domain logic (a pure, testable service) | `src/GcToolkit.Core/<Feature>/<Thing>.cs` | Yes (keep VMs thin) |
| Unit tests for that logic | `tests/GcToolkit.Core.Tests/<Feature>/<Thing>Tests.cs` | Yes (TDD) |
| `[Tool]` ViewModel deriving `ToolViewModelBase` | `src/GcToolkit.Core/ViewModels/Tools/<Tool>ViewModel.cs` | Yes |
| `<Id>_Name` + `<Id>_Tooltip` strings, EN **and** CS | `src/GcToolkit/Strings/{en,cs}/Resources.resw` | Yes (build error if missing) |
| Tool icon | `ToolIcon_<Id>` template in `src/GcToolkit/Resources/IconGeometries.xaml` | Optional (degrades gracefully) |
| View pair (`<Tool>ViewBase` + `<Tool>View`) | `src/GcToolkit/Views/<Tool>View.xaml(.cs)` | Optional (omit = placeholder stub) |
| New category | `Discovery/ToolCategory.cs` + `Category_<Member>` string + category icon | Only if no existing category fits |

## Checklist

### 1. Logic in a Core service, with tests first (TDD)

Domain logic must be unit-testable and free of UI dependencies, so it lives in `GcToolkit.Core` and
is covered in `GcToolkit.Core.Tests`. Write the failing test first, watch it fail, then implement.

- Service: `src/GcToolkit.Core/<Feature>/<Thing>.cs` (e.g. a codec / cipher / converter).
- Tests: `[TestClass]`/`[TestMethod]`, named `Method_Scenario_ExpectedResult`, MSTest `Assert`.
- Run: `dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj` (MTP runner — **never**
  pass `--nologo`; it runs zero tests).

### 2. The ViewModel

Derive `ToolViewModelBase`, decorate with `[Tool]`. The base already provides `ToolName`, `Tooltip`,
`IsFavorite`, `ToggleFavoriteCommand`, `IsPlaceholder`, and records the open in recents — keep your VM
thin (wire the service to bindings; no UI manipulation).

```csharp
[Tool("Sample", ToolCategory.Numbers,
      Introduced = "2026-06-03", Updated = "2026-06-03",
      Keywords = ["sample", "convert", "vzorek"])]   // include CS aliases for search
public sealed partial class SampleViewModel : ToolViewModelBase
{
    private readonly SampleCodec _codec = new();        // your Core service
    private readonly IClipboardService _clipboard;

    public SampleViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard)                    // tool-specific services come after the base 4
        : base("Sample", catalog, recents, favorites, localizer)
        => _clipboard = clipboard;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    partial void OnInputTextChanged(string value) => OutputText = _codec.Convert(value);
}
```

`[Tool]` rules:
- **`Id`** (first arg, also the `base(...)` arg) — unique, resource-key-safe **PascalCase**. It is the
  localization base key (`<Id>_Name` / `<Id>_Tooltip`) **and** the favorites/recents key. **Never
  rename it later** — that orphans saved favorites/recents.
- **`Introduced` / `Updated`** — ISO `yyyy-MM-dd`. A bad or missing date is a **build error**
  (`GCTOOL001` / `GCTOOL003`). A recent date earns the tool a **New/Updated** badge and a slot in
  Home's "New & Updated".
- **`Keywords`** — optional search aliases, merged with the localized name. Add Czech aliases too.

### 3. Cross the Core→head boundary with services

The VM is in Core, so it may only depend on **Core** interfaces. Any platform capability is a Core
interface implemented in the head and registered in `App.RegisterServices`. Reusable ones already
exist — inject them, don't reinvent:

| Interface (in `GcToolkit.Core/Services`) | Use for |
|---|---|
| `IClipboardService` | copy the result |
| `IShareService` | `ShareTextAsync(...)` to the OS share sheet |
| `INavigationService` | `Navigate<TViewModel>()` |
| `IMorseAudioService` | tone playback (Morse) — has `IsSupported` |
| `IDisplayRequestManager` | keep the screen awake during playback |

Add a new one the same way: interface in `GcToolkit.Core/Services`, implementation in
`GcToolkit/Services`, registered in `App.RegisterServices` (`App.xaml.cs`) with the right lifetime —
`AddScoped` for per-window services (clipboard, share, audio, navigation all are), `AddSingleton` for
app-wide state (`IDisplayRequestManager` is). Head-specific code uses `#if !HAS_UNO`
(WinAppSDK / Windows) vs `#else` (Uno heads). When a capability only works on some heads, expose an
`Is…Supported` flag on the interface and **hide its UI** where unsupported — every head must compile.

### 4. The two required strings (+ UI keys), EN and CS

Add to **both** `src/GcToolkit/Strings/en/Resources.resw` **and** `.../cs/Resources.resw`:

| Key | Required |
|---|---|
| `<Id>_Name` | Yes — missing in any culture is build error `GCTOOL004` |
| `<Id>_Tooltip` | Yes — same |
| Your UI keys (button labels, headers, …) | As needed |

Keys are short and descriptive (`SamplePlay`, `SampleSwap`) — **no `x:Uid`, no GUIDs**. In XAML use the
markup extension: `Text="{markup:Localize Key=SamplePlay}"`. Missing **UI** keys render as blank text at
runtime (not a build error), so keep EN and CS in lockstep.

### 5. Tool icon

Icons are **vector path data**, not image assets: add a `DataTemplate` keyed `ToolIcon_<Id>` to
`src/GcToolkit/Resources/IconGeometries.xaml` (keep the alphabetical order and bump the count in the
`Tools (N)` comment). `ToolIcons.CreateIcon` resolves it by key; a missing key simply renders nothing.

```xml
<DataTemplate x:Key="ToolIcon_MyTool">
    <PathIcon Data="M 6 3 L 6 6 ... z" />
</DataTemplate>
```

- Style: one **Icons8 Material Outlined** family, every path on a **24×24** box. Pick a clear metaphor
  (Morse → radio waves; Roman → a numeral glyph). Take the SVG's `d` attribute as the `Data`; if the SVG
  has a `viewBox` other than `0 0 24 24`, rescale the path rather than adding a transform.
- Single-colour only: no fills, no `Foreground`. `PathIcon` inherits the foreground, so the icon follows
  light, dark and High Contrast themes. That's why PNG/SVG assets aren't used (Uno rasterises SVG at
  build time, so it can't follow the theme).
- A `DataTemplate`, never a shared `Geometry`: the same icon shows in the pane, on the card and on the
  tool page at once, and one `Geometry` can't be owned by two `PathIcon`s.
- **Check it renders**: run the app and look at the tool's card and page header. A malformed path
  fails silently and shows a blank square.

### 6. The View (omit it to ship a stub)

For a real tool with custom UI, author the view as a **non-generic base + sealed view** pair so XAML can
close the generic. Put `[NavigationInfo(NavigationSection.Tool)]` on the base so pane selection works:

```csharp
[NavigationInfo(NavigationSection.Tool)]
public partial class SampleViewBase : ViewBase<SampleViewModel> { }

public sealed partial class SampleView : SampleViewBase
{
    public SampleView() => this.InitializeComponent();
}
```

The XAML root element is the intermediate base: `<local:SampleViewBase x:Class="…SampleView" …>`.
`ViewBase<TVm>` resolves the VM from the per-window scope and sets `DataContext` — **never** `new` a VM
or set `DataContext` by hand. Build accessible, responsive XAML: Fluent styles + theme resources,
`AutomationProperties.Name` on icon-only controls, an `AdaptiveTrigger` for padding, a `MaxWidth` for
readability. Run `/winui-design` for any layout/styling work.

**Root layout — always `ScrollViewer > Grid > StackPanel(MaxWidth, Stretch)`.** Never make a
`MaxWidth`-capped panel the `ScrollViewer`'s *direct* child, with **either** alignment:

```xml
<ScrollViewer Padding="24">
    <!--  Grid bed: full width, NO MaxWidth. Keeps DirectManipulation out of the centering.  -->
    <Grid>
        <StackPanel MaxWidth="760" HorizontalAlignment="Stretch" Spacing="16">
            …
        </StackPanel>
    </Grid>
</ScrollViewer>
```

**Why** (verified against the WinUI C++ sources and measured live):

- A `ScrollViewer`'s **direct child** is the DirectManipulation "primary content"; layout *skips*
  `ComputeAlignmentOffset` for it (`framework.cpp:1915`) and lets DManip position it.
- `HorizontalAlignment="Center"` (or any non-`Stretch`) arranges an element at its
  **`unclippedDesiredSize.width`** (`framework.cpp:1788-1791`). A vertical `StackPanel`'s desired width is
  the max of its children's, and a wrapping `TextBox`'s desired width **grows with its text** — so the
  column resizes and re-centers **on every keystroke**. It also never reaches `MaxWidth`.
- `HorizontalAlignment="Stretch"` + `MaxWidth` as the direct child renders at `MaxWidth` but DManip
  centers it against the scroll *extent* (its desired width) → off-centre + a jump. This is the won't-fix
  WinUI bug [microsoft-ui-xaml#4619](https://github.com/microsoft/microsoft-ui-xaml/issues/4619).
- The **`Grid` bed** is full-viewport-width with no `MaxWidth`, so it hits the "stretch treated as near"
  hatch (`framework.cpp:1925-1928`) — DManip does no centering at all. The inner panel is then an ordinary
  (non-manipulated) child: `Stretch` + `MaxWidth` clamps its arranged width to a **constant** `MaxWidth`
  (`framework.cpp:1806-1811`) and normal layout centers it. Width and offset depend on the **viewport**,
  never the content ⇒ zero drift, full width, correctly centred, and it degrades to full-width on narrow
  windows.

Do **not** wrap the `ScrollViewer` in an outer `<Grid MaxWidth=… HorizontalAlignment="Center">` either —
that outer element is itself content-hugging and drifts for the same reason.

**Stub form** — to land a tool before its UI exists, give it just the `[Tool]` VM (a one-liner via a
primary constructor) and **no view**. It shows in the gallery and opens the placeholder host:

```csharp
[Tool("Sample", ToolCategory.Numbers, Introduced = "2026-06-03", Updated = "2026-06-03",
      Keywords = ["sample"])]
public sealed partial class SampleViewModel(
    ICatalogService catalog, IRecentsService recents,
    IFavoriteToolsService favorites, IStringLocalizer localizer)
    : ToolViewModelBase("Sample", catalog, recents, favorites, localizer);
```

### 7. New category — only if none fits

Categories are a closed enum, metadata-by-convention. To add one:
1. **Append** a member to `ToolCategory` (`src/GcToolkit.Core/Discovery/ToolCategory.cs`) — append so
   existing display order (declaration index) is preserved.
2. Add a `Category_<Member>` string (EN + CS).
3. Add a `ToolIcon_<Member>` template under the `Categories (N)` comment in `IconGeometries.xaml` (same rules as tool icons).
4. *(Optional)* map it to a `ToolGroup` in `ToolGrouping.CategoryGroups`
   (`Discovery/ToolGroup.cs`) to render it under a pane section header, adding a `Group_<Member>` string
   if you also add a new group. A category absent from the map renders with no heading.

### 8. Build & verify

A tool touches the generator, so also run its test project. All heads must compile (gate any
head-specific capability behind `Is…Supported`).

```pwsh
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj
dotnet test tests/GcToolkit.SourceGenerators.Tests/GcToolkit.SourceGenerators.Tests.csproj
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-windows10.0.26100   # WinAppSDK head
dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop             # Skia desktop
dotnet xstyler -c Settings.XamlStyler -r -d ./src                          # XAML format (CI-enforced)
dotnet format src/GcToolkit.slnx                                           # before staging
```

To see the tool actually render and drive its UI, use the **`/run-winui-app` skill** (build → launch
packaged → inspect/click/screenshot → clean up) plus the **uno-app MCP** for runtime inspection. Don't
report a tool done before you've seen it open in the gallery.

## Feature parity — ask first, then beat it

For every tool, aim for parity-or-better with a reference implementation. **Ask the user which parity
source to compare against** (a specific site, app, or competing tool) before building — don't assume.
Also sanity-check the canonical source (e.g. Wikipedia's table) so coverage is complete.

- **Match conventions** so data interoperates with the reference (e.g. Morse: dot `.`, dash `-`, single
  space = letter gap, two+ spaces = word gap, `#` for unknown).
- **Then do better** where it's cheap — the existing tools each exceed parity (Morse adds full
  punctuation/accents + audio + visual flash; Caesar adds a "show all shifts" brute-force view; Roman
  adds the vinculum range + lenient decoding with warnings). Capture the comparison in the PR.
- Aim to cover all features the parity source has, and then exceed it with additional features (especially when the device capabilities like sensors would provide even better experiences than the reference).

## Common mistakes

| Mistake | Fix |
|---|---|
| Putting the VM or logic in the head | VMs and domain logic go in `GcToolkit.Core` so they're testable; only impls live in the head |
| Renaming `Id` after release | Don't — it's the favorites/recents/localization key. Orphans saved state |
| String added to EN only | Add `<Id>_Name`/`<Id>_Tooltip` to **both** cultures or the build fails (`GCTOOL004`) |
| Forgetting `[NavigationInfo(NavigationSection.Tool)]` | Pane selection breaks; put it on the `ViewBase` |
| VM reaching into controls | VMs interact only through bindings + service interfaces (head-independent) |
| Editing `App.xaml.cs`/nav tables to register the tool | Unnecessary — discovery is automatic. Only register a **new service** in DI |
| A head-specific API with no guard | Gate behind `Is…Supported`, hide its UI elsewhere, or the other heads won't compile |
| Hand-editing version numbers | Versions come from Nerdbank.GitVersioning — never touch manifests |

## Anchor types (don't guess these)

- `ToolViewModelBase` (`GcToolkit.Core/ViewModels/`) — base ctor is
  `(string toolId, ICatalogService, IRecentsService, IFavoriteToolsService, IStringLocalizer)`.
- `[Tool]` (`GcToolkit.Core/Discovery/ToolAttribute.cs`) — `(string id, ToolCategory category)` +
  `Introduced`, `Updated`, `Keywords`.
- `ToolCategory` / `ToolGroup` / `ToolGrouping` (`GcToolkit.Core/Discovery/`).
- `ViewBase<TVm>`, `[NavigationInfo]` (head) — the view contract the generator routes to.
