# Versioning

> `main` is where the next release is brewing; `release/v{minor}` branches are where releases ship.
> Every build carries an `AppChannel` (Dev for local + main, Prod for release/v*) baked into its package identity so Dev and Prod sit side-by-side on every device.

## Branch model

| Branch | `version.json` | NBGV-computed | Manifest version | Channel |
|---|---|---|---|---|
| `main` | next planned minor (e.g. `0.2`) | `0.2.0-dev.{height}` | `0.2.0.{height}` | Dev |
| `release/v0.1` | `0.1` | `0.1.0`, `0.1.1`, … | `0.1.0.0`, `0.1.1.0`, … | Prod |
| `feature/*` | inherited from main | `0.2.0-dev.{height}+{sha}` | (not packaged) | Dev |

`publicReleaseRefSpec` matches `release/v{minor}` only, so main can never produce a stable version by accident. `firstUnstableTag` is `dev`.

## AppChannel

Single MSBuild property: `AppChannel = Dev | Prod`. Defaults to `Dev`. Every CI packaging workflow sets the value from the branch (release/v* → Prod, else Dev) and passes `/p:AppChannel=...` to the build.

What changes per channel:
- **Identity:** Android `applicationId`, iOS Bundle ID — `…gctoolkit` vs `…gctoolkit.dev`. Windows Identity Name is set by the signing certificate's Publisher CN (Prod cert vs Dev self-signed cert).
- **Display name:** `GC Toolkit` vs `GC Toolkit Dev`.
- **App icon:** the Dev channel overrides the icon *foreground* (`Assets/Icons/icon_foreground.svg` vs `icon_foreground_dev.svg`); the background is shared. Uno derives the generated icon resource name from the background file, so the background must stay constant across channels — only the foreground changes.
- **In-app banner:** a `DEV` corner badge appears when `AppEnvironment.IsDevChannel` is true.
- **Compile constant:** `APP_CHANNEL_DEV` defined on Dev builds.
- **Appsettings:** `appsettings.Dev.json` (overlay placeholder; wire up to your configuration host if you need separate sandbox keys).

## Workflows

| Workflow | Trigger | Channel | Publishes? |
|---|---|---|---|
| `ci.yml` | push: main, PR: main + release/** | n/a (Debug smoke test) | n/a |
| `validate-manifest-version.yml` | push: main, PR: main + release/** | n/a | n/a |
| `package-windows.yml` | push: main + release/v* + workflow_dispatch | Dev on main (self-signed), Prod on release/v* (Store-signed) | No — manual Partner Center upload from artifact |
| `package-android.yml` | push: main + release/v* + workflow_dispatch | Dev applicationId on main, Prod on release/v* | release/v* → Play Production track via `r0adkll/upload-google-play` |
| `package-ios.yml` | push: main + release/v* + workflow_dispatch | Dev Bundle ID on main, Prod on release/v* | release/v* → TestFlight via `apple-actions/upload-testflight-build` (manual "Submit for Review") |
| `tag-release.yml` | workflow_run after the three packaging workflows on release/v* | n/a | Pushes annotated tag `v{version}` |
| `prepare-release.yml` | workflow_dispatch | n/a | Cuts release/v{current}, opens PR for main bump |
| `wasm-deploy.yml` | push: release/v*, PR: main | Prod on release/v*, Dev on PR previews | release/v* → live Cloudflare Worker; PRs → `pr-<number>` preview version (public repos only). See [deployment.md](deployment.md) |

All packaging workflows are gated by `github.event.repository.private == false || github.event_name == 'workflow_dispatch'` so private forks don't burn paid Actions minutes on every push.

## Release lifecycle

### Cutting a new release

**Local (canonical):**
```pwsh
nbgv prepare-release
git push origin main release/v{current}
```

**Workflow:** Actions → Prepare Release → Run. Pushes release branch, opens a PR for the main bump.

### Building releases

Push to `release/v0.2` → packaging workflows fire (subject to the public/private gate) → Prod identity, Store-signed → Android auto-publishes to Play Production, iOS auto-uploads to TestFlight (manual review submission), Windows produces a `.msixupload` for manual Partner Center upload → `tag-release.yml` pushes `v0.2.0` after all three succeed.

### Patching a release

Commit fix to `release/v0.2`. NBGV bumps patch (`0.2.1`). Same workflows fire, new tag.

If the fix also belongs on main: cherry-pick or merge `release/v0.2` → main.

### Mainline development

Push to main. NBGV stamps `0.3.0-dev.{height}` (assuming the most recent cut bumped main to 0.3). Workflows produce Dev-identity artifacts; nothing is published anywhere.

### Major version bump

Set `version.json` on main to `1.0` manually instead of running `prepare-release`. NBGV doesn't enforce a major-bump policy.

## Secrets reference

| Secret | Purpose | Consumed by |
|---|---|---|
| `BASE64_ENCODED_WINDOWS_PFX` | Prod Windows signing cert (Store-trusted) | `package-windows.yml` on release/v* |
| `BASE64_ENCODED_WINDOWS_PFX_DEV` | Dev Windows signing cert (self-signed; install as trusted root on test machines) | `package-windows.yml` on main |
| `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_STORE_PASSWORD` | Android signing | `package-android.yml`: required on release/v*, optional on main (falls back to the debug key) |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Play Console API auth | `package-android.yml` on release/v* (Play Production upload) |
| `APPLE_DISTRIBUTION_P12_BASE64`, `APPLE_P12_PASSWORD`, `APPLE_PROVISIONING_PROFILE_BASE64`, `APPLE_PROVISIONING_PROFILE_UUID`, `APPLE_CODESIGN_KEY`, `APPLE_TEAM_ID` | iOS distribution signing | `package-ios.yml` on release/v* |
| `APPSTORE_ISSUER_ID`, `APPSTORE_API_KEY_ID`, `APPSTORE_API_PRIVATE_KEY` | App Store Connect API auth | `package-ios.yml` on release/v* (TestFlight upload) |

## Why is build `0.X.0-dev.5` instead of `0.X.0`?

Because the branch isn't a release branch. NBGV's `publicReleaseRefSpec` only matches `^refs/heads/release/v\d+\.\d+$`. The `-dev.{height}` suffix indicates a prerelease build with the commit-height since the last `version.json` bump.

## Why is the Dev app a separate install from the Store version?

Because their package identities differ: `dev.mzikmund.gctoolkit.dev` vs `dev.mzikmund.gctoolkit` on Android and iOS, and `GcToolkit.Dev`/Dev-Publisher-CN vs `GcToolkit`/Prod-Publisher-CN on Windows. Different identities mean separate AppData, separate user state, no upgrade collision.
