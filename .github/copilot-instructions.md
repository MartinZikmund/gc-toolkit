# GC Toolkit — instructions for coding agents

GC Toolkit is a free, open-source geocaching puzzle-solving toolkit: a gallery of offline solving
tools (ciphers, coordinate maths, number sequences, reference alphabets, text utilities). It is an
Uno Platform single-project app on .NET 10, targeting Windows (WinAppSDK), Android, iOS, desktop
(Windows/macOS/Linux) and WebAssembly.

**Read [`AGENTS.md`](../AGENTS.md) first** — it is the authoritative guidance for this repository, and
the detailed conventions it points at live in [`.claude/rules/`](../.claude/rules):
`architecture.md`, `code-style.md`, `testing.md`, `git.md`. Adding a tool to the gallery has its own
workflow in [`.claude/skills/building-gc-tools/SKILL.md`](../.claude/skills/building-gc-tools/SKILL.md).
Human-facing setup and contribution guidance is in [`CONTRIBUTING.md`](../CONTRIBUTING.md).

The few things worth restating inline:

## Build one head at a time

```bash
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-desktop
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-windows10.0.26100
```

Use `-p:SingleTargetFramework=`, **not** a bare `-f`. `-f` narrows only the build; the implicit
restore still evaluates every target framework and fails with `NETSDK1147`, demanding the
android/ios/wasm workloads.

## Test

```bash
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj -p:SingleTargetFramework=net10.0
```

MSTest on the **Microsoft Testing Platform** — do not pass VSTest-only flags such as `--nologo` or
`--logger`; MTP rejects them and runs zero tests. TDD is the working style: failing test first.

## Core / head split

- `GcToolkit.Core` — view models, service interfaces, models. **Default here**, so logic stays
  unit-testable without a UI head. Core references Uno.UI, so using a WinUI *type* is not a reason to
  move code out of Core.
- `GcToolkit` (the head) — Views, service implementations, app composition.

MVVM is CommunityToolkit.Mvvm (`[ObservableProperty]` partial properties, `[RelayCommand]`), DI is
registered in `App.RegisterServices`, navigation is type-driven via `INavigationService`.

## Localize everything

Never hardcode a user-facing string. Every key goes in **both**
`src/GcToolkit/Strings/en/Resources.resw` and `src/GcToolkit/Strings/cs/Resources.resw`. XAML uses
`{markup:Localize Key=MyKey}`; code injects `IStringLocalizer`.

## XAML Styler is CI-enforced

Run `dotnet xstyler -c Settings.XamlStyler -r -d ./src` before committing XAML, or the **XAML Style
Check** workflow fails the PR.

## Other conventions

- File-scoped namespaces, nullable enabled, `_camelCase` private fields, braces always, expression
  bodies for one-line members, modern C#.
- Package versions live only in `src/Directory.Packages.props` (Central Package Management).
- Conventional Commits (`feat:`, `fix:`, `chore:`, …); work on `feature/*` branches off `main`.
- Versions are computed by Nerdbank.GitVersioning — never hand-edit a version number.
- This app is plain WinUI/XAML + CommunityToolkit.Mvvm by design: do not introduce Uno.Extensions
  Navigation, C# Markup, or MVUX.
