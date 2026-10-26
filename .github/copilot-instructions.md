# GcToolkit
GcToolkit is a cross-platform Uno Platform application built with .NET 10.0 that runs on Android, iOS, Desktop, WebAssembly, and WinUI. It provides gctoolkit functionality with lap tracking, history, and customizable interface.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively
- Bootstrap, build, and test the repository:
  - Install .NET 10.0 SDK: `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0`
  - Export PATH: `export PATH="$HOME/.dotnet:$PATH"`
  - Install uno-check tool: `dotnet tool install --global --version 1.28.3 uno.check`
  - Install workloads: `~/.dotnet/tools/uno-check --ci --fix --non-interactive --verbose --skip openjdk` -- takes 6-7 minutes. NEVER CANCEL. Set timeout to 15+ minutes.
- Build commands:
  - WebAssembly (works): `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-browserwasm` -- takes 5 seconds
  - Desktop (works): `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop` -- takes 4 seconds
  - DO NOT attempt full multi-target build: `dotnet build src/GcToolkit/GcToolkit.csproj` -- FAILS due to network restrictions blocking Android dependencies from dl.google.com
- No formal unit tests exist in this repository.
- Code formatting: `dotnet format src/GcToolkit/GcToolkit.csproj` -- the code currently has formatting violations but the command works

## Validation
- Build for specific target frameworks only (WebAssembly or Desktop) to avoid network dependency issues.
- ALWAYS test the WebAssembly build by running it in browser:
  - Navigate to: `cd src/GcToolkit/bin/Debug/net10.0-browserwasm/wwwroot`
  - Start server: `python3 -m http.server 8080`
  - Open: `http://localhost:8080/`
  - Verify the gctoolkit interface loads and displays "00:00:00.00"
- You can build and run the Desktop version but cannot interact with its UI in this environment.
- ALWAYS run `dotnet format src/GcToolkit/GcToolkit.csproj --verify-no-changes` to check for formatting issues before committing.

## Common tasks
The following are outputs from frequently run commands. Reference them instead of viewing, searching, or running bash commands to save time.
````instructions
# GcToolkit
GcToolkit is a cross-platform Uno Platform application built with .NET 10.0 that runs on Android, iOS, Desktop, WebAssembly, and WinUI. It demonstrates a typical Uno-based app structure and common build/runtime workflows.

Always reference these instructions first. Use command-line checks or CI logs only when behavior differs from what's described here.

## Working Effectively
- Bootstrap, build, and test the repository:
  - Install .NET 10.0 SDK: `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0`
  - Export PATH: `export PATH="$HOME/.dotnet:$PATH"`
  - Install `uno-check` tool: `dotnet tool install --global --version 1.28.3 uno.check`
  - Install workloads: `~/.dotnet/tools/uno-check --ci --fix --non-interactive --verbose --skip openjdk` -- this may take several minutes. Allow ample time (15+ minutes on slow connections).
- Build commands (target single frameworks to avoid external dependency/network issues):
  - WebAssembly: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-browserwasm`
  - Desktop: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-desktop`
  - Avoid a full multi-target build in constrained environments: `dotnet build src/GcToolkit/GcToolkit.csproj` may fail if network access to platform SDKs is restricted.
- Unit tests: this template may not include formal unit tests; add tests under `tests/` if needed.
- Code formatting: `dotnet format src/GcToolkit/GcToolkit.csproj` and verify with `--verify-no-changes` before committing.

## Validation
- Prefer building for a single target framework (WebAssembly or Desktop) when validating local changes.
- To validate WebAssembly output locally:
  - Navigate to: `cd src/GcToolkit/bin/Debug/net10.0-browserwasm/wwwroot`
  - Start a simple HTTP server: `python3 -m http.server 8080`
  - Open: `http://localhost:8080/` and confirm the application UI loads without console errors.
- Desktop builds can be produced locally but may not be runnable in all CI environments.
- Always run `dotnet format src/GcToolkit/GcToolkit.csproj --verify-no-changes` before creating a commit or PR.

## Common tasks
The following captures the repository layout and common commands to speed onboarding and troubleshooting.

### Project Structure
```
src/
├── GcToolkit/
│   ├── GcToolkit.csproj          # Main Uno Platform project
│   ├── App.xaml                    # Application entry point
│   ├── WindowShell.xaml            # Main window shell
│   ├── Assets/                     # Images and resources
│   ├── Views/                      # XAML pages and controls
│   ├── ViewModels/                 # MVVM view models
│   ├── Services/                   # Application services
│   ├── Models/                     # Data models
│   ├── Platforms/                  # Platform-specific code
│   └── Properties/                 # Project properties
├── GcToolkit.slnx                # Solution file
├── Directory.Build.props           # Shared build properties
├── Directory.Packages.props        # Centralized package management
└── global.json                     # .NET SDK version
```

### Key Files
- `src/GcToolkit/GcToolkit.csproj` - Main project targeting multiple platforms
- `src/.editorconfig` - Code style rules
- `.github/workflows/ci.yml` - CI pipeline (may target Windows, Linux, or macOS depending on setup)
- `.github/workflows/package-windows.yml` - Windows packaging workflow (if present)

### Target Frameworks
- `net10.0-windows10.0.26100` - Windows (requires Windows SDK)
- `net10.0-android` - Android
- `net10.0-ios` - iOS
- `net10.0-desktop` - Cross-platform desktop
- `net10.0-browserwasm` - WebAssembly

### Dependencies (examples commonly found in Uno projects)
- Uno Platform SDK
- CommunityToolkit.Mvvm for MVVM (CommunityToolkit.WinUI was dropped: its Uno build is not yet compatible with Uno 7)
- Optional: local storage or database libraries (e.g., LiteDB) and graphics libraries (e.g., SkiaSharp)

### Build Timing Expectations
- `uno-check` workload installation: a few minutes (allow 10-15 minutes on slower hosts)
- WebAssembly build: typically fast for this template
- Desktop build: typically fast for this template

### Common Errors and Solutions
- Errors trying to download Android/iOS SDK components: may be caused by restricted network environments. Build single-target frameworks to avoid these failures.
- Package version warnings: often informational, do not always block builds.
- Formatting/whitespace issues: run `dotnet format` to resolve.

### Validation Scenarios
After making code changes, a sensible validation flow is:
1. Build for WebAssembly: `dotnet build src/GcToolkit/GcToolkit.csproj -f net10.0-browserwasm`
2. Serve and open the WebAssembly output in a browser; confirm the app UI loads and key pages render without errors
3. Check formatting: `dotnet format src/GcToolkit/GcToolkit.csproj --verify-no-changes`
4. Manually exercise core user flows relevant to the changes (navigation, data loading, settings, persistence)

### CI/CD Information
- CI may target platform-specific runners; check the workflow files in `.github/workflows/` for details.
- Builds use MSBuild/dotnet CLI: `msbuild ./src/GcToolkit/GcToolkit.csproj /r` or `dotnet build`
- Packaging for Windows (MSIX) or platform-specific artifacts is handled by dedicated workflows when present.
````