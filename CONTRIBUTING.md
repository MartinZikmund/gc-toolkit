# Contributing to GC Toolkit

Thanks for wanting to help. Bug reports, tool ideas, translations and pull requests are all welcome.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). Contributions are licensed
under the project's [GPLv3](LICENSE). Found a security problem? Please don't open a public issue —
see [SECURITY.md](SECURITY.md).

> AI coding agents: the machine-readable version of everything below lives in
> [`AGENTS.md`](AGENTS.md) and [`.claude/rules/`](.claude/rules). Read those first.

---

## Prerequisites

- The .NET SDK pinned in [`global.json`](global.json) (`allowPrerelease` is off, so a released SDK).
- The workload for whichever platform you build — Windows needs the Windows SDK / WinAppSDK, wasm
  needs `wasm-tools`, Android needs the Android workload and a JDK, iOS needs a Mac.
  [`uno-check`](https://aka.ms/uno.check) is the quickest way to get a machine into shape.
- The repo's local dotnet tools (XAML Styler), restored once after cloning:

  ```bash
  dotnet tool restore
  ```

## Building

Build **one head at a time** with `-p:SingleTargetFramework=`:

```bash
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-desktop
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-windows10.0.26100
```

### Why not just `-f`?

`GcToolkit` and `GcToolkit.Core` multi-target android/ios/wasm/desktop/windows. `dotnet build -f
<tfm>` narrows only the **build** — the implicit **restore** still evaluates every target framework
and fails with `NETSDK1147`, demanding the android/ios/wasm workloads even for a desktop or Windows
build. `SingleTargetFramework` rewrites `<TargetFrameworks>` to the one target framework, so restore
stays on that platform too. It is opt-in: only the cross-targeted Uno projects read it.

For repeated local builds, copy [`src/crosstargeting-override.props.sample`](src/crosstargeting-override.props.sample)
to `src/crosstargeting-override.props` (gitignored) and set the target framework there instead of
passing the flag every time.

### Running the Windows head

The WinAppSDK head can be built, launched fully packaged, and UI-automated from the command line —
no Visual Studio needed. The happy path, gotchas and the full `winapp ui` command set are in
[`.claude/skills/run-winui-app/SKILL.md`](.claude/skills/run-winui-app/SKILL.md).

## Tests

Logic worth testing lives in **`GcToolkit.Core`** and its tests in **`GcToolkit.Core.Tests`** — keep
testable code there so it stays head-independent.

```bash
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj -p:SingleTargetFramework=net10.0
```

The runner is the **Microsoft Testing Platform**, not VSTest, so **don't pass VSTest-only flags**
like `--nologo` or `--logger` — MTP rejects them and runs zero tests. Filter with
`--filter "FullyQualifiedName~MyClass"`.

Conventions: name tests `Method_Scenario_ExpectedResult`, structure them Arrange / Act / Assert, use
`[TestMethod]` and `[DataRow]`, assert with the built-in MSTest `Assert` API, and prefer small
hand-written fakes over a mocking framework (see `Fakes/` in the test project). We aim for TDD —
write the failing test first.

## Architecture

### The Core / head split

- **`GcToolkit.Core`** — view models, service *interfaces*, models, `ViewModelBase`. Core references
  Uno.UI, so WinUI/Uno UI *types* (e.g. `ElementTheme`) are fair game here — using a UI type is not a
  reason to push a view model into the head.
- **`GcToolkit`** (the head) — Views, platform/service *implementations*, app composition (`App`,
  `WindowShell`).

**Default to Core** for view models and logic so they are unit-testable without a UI head. A view
model belongs in the head only when it depends on a type or service defined in the head.

View models derive from `ViewModelBase : ObservableObject`, use `[ObservableProperty]` partial
properties and `[RelayCommand]`, and override the lifecycle hooks (`ViewCreated`, `ViewLoaded`,
`OnNavigatedTo`, …) instead of wiring page events. A view model never touches controls directly — if
it genuinely must drive its view, define a small view-service interface in Core and let the View
implement it.

Everything is registered in `App.RegisterServices`; scope validation is on, so lifetimes have to be
right. For decoupled cross-view-model communication use CommunityToolkit.Mvvm's
`WeakReferenceMessenger`.

Full detail: [`.claude/rules/architecture.md`](.claude/rules/architecture.md).

### `IViewBase`

`ViewBase<TViewModel>` is the base class for views (pages). It resolves the view model from the
hosting `WindowShell` service provider and forwards lifecycle events to it.

`IViewBase` is a non-generic interface that exposes the resolved view model as `object?`:

```csharp
public interface IViewBase
{
    object? ViewModel { get; }
}
```

Every `ViewBase<TViewModel>` implements `IViewBase`, so a view can be referenced through the
interface without depending on its concrete view model type.

Use it when:

- A `DataTemplate` (or other loosely-typed code) needs to reach a view's view model without knowing
  its generic argument.
- A test needs to inspect a view's resolved view model without knowing its concrete type at compile
  time.

Because XAML can't reference a generic base, each view is authored as a pair: a non-generic
intermediate base that closes the generic (`public partial class XViewBase : ViewBase<XViewModel>`)
and the sealed view itself (`public sealed partial class XView : XViewBase`), with the XAML rooted at
`<local:XViewBase x:Class="…XView" …>`.

### Adding a tool

Tools are discovered at **compile time** by `GcToolkit.SourceGenerators`, so adding one needs no edit
to any shared file — no DI line, no `App.xaml.cs`, no navigation table. You add a `[Tool]`-decorated
view model in `GcToolkit.Core`, its `<Id>_Name` / `<Id>_Tooltip` strings (English **and** Czech), an
icon, and a `ViewBase<TVm>` View pair in the app head.

The full step-by-step workflow is in
[`.claude/skills/building-gc-tools/SKILL.md`](.claude/skills/building-gc-tools/SKILL.md); the
discovery system itself is specified in
[`specs/002-tool-discovery/plan.md`](specs/002-tool-discovery/plan.md).

## Code style

Mirrors [`.editorconfig`](.editorconfig) and `src/Directory.Build.props` — see
[`.claude/rules/code-style.md`](.claude/rules/code-style.md) for the reasoning.

- `LangVersion=preview`, `Nullable=enable`, `ImplicitUsings=enable`. Reach for modern C# (collection
  expressions, pattern matching, primary constructors, extension members) where it reads better.
- File-scoped namespaces. System usings first. No `this.` qualification.
- `_camelCase` for private/internal fields (including `static readonly`); PascalCase types and
  members; `I`-prefixed interfaces.
- Target-typed `new()` when the type is on the left (`List<string> items = new();`), `var` when it
  isn't.
- Always use braces, even for one-line `if`/`for`/`foreach`. Prefer expression bodies for one-line
  members.
- Keep new code warning-clean; fix the cause rather than suppressing it.
- Comment the *why* in a line or two. No walls of text.

### Never hardcode user-facing strings

Every string the user can see gets a key in **both**
`src/GcToolkit/Strings/en/Resources.resw` and `src/GcToolkit/Strings/cs/Resources.resw`. In XAML use
`{markup:Localize Key=MyKey}` (the `markup` prefix maps to `using:GcToolkit.Markup`); in code inject
`IStringLocalizer` or use `Localizer.Instance["MyKey"]`. Missing UI keys render as blank text at
runtime rather than failing the build, so keep the two files in lockstep.

### Dependencies — Central Package Management

Package versions live **only** in `src/Directory.Packages.props`. Add or bump a package there with
`<PackageVersion Include="..." Version="..." />` and reference it from a `.csproj` with
`<PackageReference Include="..." />` and **no** `Version=` attribute.

### Tech choices to avoid

This app is plain WinUI/XAML + CommunityToolkit.Mvvm by design. Please don't introduce
Uno.Extensions Navigation, C# Markup, or MVUX, and prefer a WinUI / CommunityToolkit alternative over
Uno.Extensions, Uno Toolkit or Uno Themes whenever one exists. (The hosting / configuration /
localization / serialization features already wired up in `UnoFeatures` are fine to keep — this is
about not adding *new* dependencies on those stacks.)

## XAML Styler

XAML formatting is kept consistent with [XAML Styler](https://github.com/Xavalon/XamlStyler), pinned
as a local dotnet tool. [`Settings.XamlStyler`](Settings.XamlStyler) at the repository root holds the
rules (aligned with the Uno Platform and Windows Community Toolkit conventions).

From the repository root, format all XAML under `src/`:

```bash
dotnet xstyler -c Settings.XamlStyler -r -d ./src
```

Or verify without writing changes:

```bash
dotnet xstyler -c Settings.XamlStyler -r -d ./src --passive
```

### Enforcement

Formatting is enforced on every pull request by the **XAML Style Check** workflow
([`.github/workflows/xaml-style-check.yml`](.github/workflows/xaml-style-check.yml)). If a PR
contains unformatted XAML the check fails, uploads a `xaml-style-patch` artifact, and comments with
how to fix it:

- **Branches in this repo:** comment `/apply-xaml-style` on the PR and a bot
  ([`.github/workflows/xaml-style-apply.yml`](.github/workflows/xaml-style-apply.yml)) formats the
  XAML and pushes `chore: Apply XAML styler` to the PR branch.
- **Forks:** download the `xaml-style-patch` artifact and apply it locally
  (`git apply xaml-style.patch`), or re-run the formatter command above and commit the result.

The formatter rewrites any file it finds drifted, not just yours — stage only your own changes.

## Commits, branches and pull requests

- **Conventional Commits**: `type: subject`, where type is
  `feat | fix | chore | docs | refactor | test | ci | build`. A scope is optional — add one when it
  sharpens intent (`feat(navigation): …`), omit it otherwise (`fix: …`). Imperative mood, concise, no
  trailing period.
- Work on `feature/<short-name>` branches off `main` and open a PR back to `main`. Releases happen on
  `release/v{major}.{minor}` branches.
- **Don't hand-edit version numbers.** Nerdbank.GitVersioning computes them from `version.json` and
  git height. See [docs/versioning.md](docs/versioning.md).
- Before opening a PR: the tests pass, the XAML is styled, and the head you touched builds.

See [`.claude/rules/git.md`](.claude/rules/git.md) for the full convention.

## Translations

The app ships English and Czech. Both files must carry the same keys:

- `src/GcToolkit/Strings/en/Resources.resw`
- `src/GcToolkit/Strings/cs/Resources.resw`

A new language is a new folder under `src/GcToolkit/Strings/` with a translated `Resources.resw`.
Corrections to the existing Czech translation are just as welcome as new languages — open a PR or an
issue.
