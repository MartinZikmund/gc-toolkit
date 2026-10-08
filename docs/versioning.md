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
- **Identity:** all three come from the `ApplicationId` MSBuild property — Android `applicationId`, iOS Bundle ID, and the Windows manifest `Identity/Name` (`dev.mzikmund.gctoolkit` vs `dev.mzikmund.gctoolkit.dev`). The Windows `Identity/Publisher` comes from `ApplicationPublisher` (`O=Martin Zikmund`), **not** from the signing certificate; the certificate only has to match it.
- **Display name:** `GC Toolkit` vs `GC Toolkit Dev`.
- **App icon:** the Dev channel overrides the icon *foreground* (`Assets/Icons/icon_foreground.svg` vs `icon_foreground_dev.svg`); the background is shared. Uno derives the generated icon resource name from the background file, so the background must stay constant across channels — only the foreground changes.
- **In-app banner:** a `DEV` corner badge appears when `AppEnvironment.IsDevChannel` is true.
- **Compile constant:** `APP_CHANNEL_DEV` defined on Dev builds.
- **Appsettings:** `appsettings.Dev.json` (overlay placeholder; wire up to your configuration host if you need separate sandbox keys).

## Workflows

| Workflow | Trigger | Channel | Publishes? |
|---|---|---|---|
| `pr.yml` | PR: main + release/** | n/a (Debug smoke of the desktop + WinAppSDK heads; unit tests ride the desktop leg) | n/a |
| `main.yml` | push: main | n/a (Release build of every target, build-only — no signing or packaging) | n/a |
| `validate-manifest-version.yml` | push: main, PR: main + release/** — only when `Package.appxmanifest` changes | n/a | n/a |
| `package-windows.yml` | push: main + release/v* + workflow_dispatch | Dev on main (self-signed), Prod on release/v* (Store-signed) | No — manual Partner Center upload from artifact |
| `package-android.yml` | push: main + release/v* + workflow_dispatch | Dev applicationId on main, Prod on release/v* | release/v* → Play Production track via `r0adkll/upload-google-play` |
| `package-ios.yml` | push: main + release/v* + workflow_dispatch | Dev Bundle ID on main, Prod on release/v* | release/v* → TestFlight via `apple-actions/upload-testflight-build` (manual "Submit for Review") |
| `tag-release.yml` | workflow_run after the packaging workflows on release/v* | n/a | Pushes annotated tag `v{version}` once every *required* platform is green — all three by default, narrowable with the `REQUIRED_RELEASE_PLATFORMS` repository variable |
| `prepare-release.yml` | workflow_dispatch | n/a | Cuts release/v{current}, opens PR for main bump |
| `static-web-apps-deploy.yml` | push: release/v*, PR: main | Prod on release/v*, Dev on PR previews | release/v* → production Static Web App; PRs → Azure SWA preview env (public repos only) |

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

Push to `release/v0.2` → packaging workflows fire (subject to the public/private gate) → Prod identity, Store-signed → Android auto-publishes to Play Production, iOS auto-uploads to TestFlight (manual review submission), Windows produces a `.msixupload` for manual Partner Center upload → `tag-release.yml` pushes `v0.2.0` once every required platform succeeds.

### Patching a release

Commit fix to `release/v0.2`. NBGV bumps patch (`0.2.1`). Same workflows fire, new tag.

If the fix also belongs on main: cherry-pick or merge `release/v0.2` → main.

### Mainline development

Push to main. NBGV stamps `0.3.0-dev.{height}` (assuming the most recent cut bumped main to 0.3). Workflows produce Dev-identity artifacts; nothing is published anywhere.

### Major version bump

Set `version.json` on main to `1.0` manually instead of running `prepare-release`. NBGV doesn't enforce a major-bump policy.

## Secrets reference

> **None of these secrets are configured on the repository yet** — the table is aspirational, and
> describes what each workflow *expects*. Until they exist, the packaging workflows fail at their
> signing steps. See [release-runbook.md](release-runbook.md) for how to ship a Windows-only first
> release while the Android and iOS secrets are still missing.

| Secret | Purpose | Consumed by |
|---|---|---|
| `BASE64_ENCODED_WINDOWS_PFX` | Prod Windows signing cert (Store-trusted) | `package-windows.yml` on release/v* |
| `BASE64_ENCODED_WINDOWS_PFX_DEV` | Dev Windows signing cert (self-signed; install as trusted root on test machines) | `package-windows.yml` on main |
| `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`, `ANDROID_STORE_PASSWORD` | Android signing | All `package-android.yml` runs |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Play Console API auth | `package-android.yml` on release/v* (Play Production upload) |
| `APPLE_DISTRIBUTION_P12_BASE64`, `APPLE_P12_PASSWORD`, `APPLE_PROVISIONING_PROFILE_BASE64`, `APPLE_PROVISIONING_PROFILE_UUID`, `APPLE_CODESIGN_KEY`, `APPLE_TEAM_ID` | iOS distribution signing | `package-ios.yml` on release/v* |
| `APPSTORE_ISSUER_ID`, `APPSTORE_API_KEY_ID`, `APPSTORE_API_PRIVATE_KEY` | App Store Connect API auth | `package-ios.yml` on release/v* (TestFlight upload) |

## Why is build `0.X.0-dev.5` instead of `0.X.0`?

Because the branch isn't a release branch. NBGV's `publicReleaseRefSpec` only matches `^refs/heads/release/v\d+\.\d+$`. The `-dev.{height}` suffix indicates a prerelease build with the commit-height since the last `version.json` bump.

## Why is the Dev app a separate install from the Store version?

Because their package identities differ: `dev.mzikmund.gctoolkit.dev` vs `dev.mzikmund.gctoolkit` — on Android and iOS, and equally in the Windows manifest's `Identity/Name`. Different identities mean separate AppData, separate user state, no upgrade collision.
