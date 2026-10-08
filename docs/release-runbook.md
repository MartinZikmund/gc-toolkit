# Release runbook

How a GC Toolkit release actually ships. Written to be followed once and then forgotten — every step
names the exact workflow, file or Partner Center page involved.

The versioning *model* (branches, channels, identities) is in [versioning.md](versioning.md); this
document is the *procedure*.

> **Status: the first Store release has not happened yet.** Steps that need a value which does not
> exist yet are marked **TODO** and must be filled in before or during that first release.

---

## Checklist

Copy this into the release issue and tick as you go.

```text
PRE-FLIGHT
[ ] main is green (Main workflow) and there is nothing half-finished you don't want shipped
[ ] Tests pass locally:  dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj -p:SingleTargetFramework=net10.0
[ ] XAML is styled:      dotnet xstyler -c Settings.XamlStyler -r -d ./src --passive
[ ] version.json on main is the version you intend to release
[ ] Package.appxmanifest Identity Version is still 0.0.0.0 (NBGV stamps the real one)
[ ] README / PRIVACY / THIRD-PARTY-NOTICES are current for what is shipping
[ ] TODO(first release): Store identity settled — see "Store identity" below. BLOCKER.
[ ] TODO(first release): GitHub Actions secrets configured — see "Secrets" below. BLOCKER.
[ ] TODO(first release): privacy policy URL decided and reachable
[ ] Repo is public (packaging workflows are gated on it) — or plan to use workflow_dispatch

CUT THE RELEASE BRANCH
[ ] Actions -> "Prepare Release" -> Run workflow (on main)
[ ] release/v{x.y} exists on origin
[ ] Merge the "chore: bump version after cutting release/v{x.y}" PR into main

BUILD
[ ] Build Windows Packages   green on release/v{x.y}
[ ] Build Android Packages   green (or excluded via REQUIRED_RELEASE_PLATFORMS)
[ ] Build iOS Packages       green (or excluded via REQUIRED_RELEASE_PLATFORMS)
[ ] Azure Static Web Apps CI/CD green (the web build)
[ ] Download the .msixupload artifact from the Windows run

PARTNER CENTER
[ ] Pricing and availability: Free, markets chosen, Visibility = PRIVATE AUDIENCE
[ ] Age ratings: IARC questionnaire completed
[ ] Properties: category, privacy policy URL, support contact, product declarations
[ ] Store listings: English (United States) and Czech
[ ] Store listing -> Additional license terms: GPLv3 text/link
[ ] Packages: upload the .msixupload, no errors
[ ] Submit -> certification passes -> install from the Store on a clean machine and smoke-test
[ ] New submission: Visibility -> Public audience -> submit

AFTER
[ ] tag-release.yml pushed v{x.y.z} (check the tags); if not, tag manually
[ ] GitHub Release created from the tag with notes
[ ] README Store badge uncommented with the real product URL
[ ] AppRatingService store ID replaced (see "Loose ends")
[ ] Watch the Partner Center Health report for the first week
```

---

## 0. Prerequisites that do not exist yet

### Store identity — **TODO, and a hard blocker**

When you reserve the app name in Partner Center, Microsoft assigns the package identity: a
**Package/Identity/Name** (something like `12345MartinZikmund.GCToolkit`) and a
**Package/Identity/Publisher** (`CN=<your publisher GUID>`). Partner Center rejects a package whose
manifest does not match both.

Today the manifest is generated from MSBuild properties in `src/GcToolkit/GcToolkit.csproj`:

| Manifest field | Comes from | Current Prod value |
| --- | --- | --- |
| `Identity/Name` | `ApplicationId` | `dev.mzikmund.gctoolkit` |
| `Identity/Publisher` | `ApplicationPublisher` | `O=Martin Zikmund` |
| `DisplayName` | `ApplicationTitle` | `GC Toolkit` |

Neither identity value can match a reserved Store identity by luck. **Before the first release**,
reserve the name, then decide how the Prod build picks up the Store's values — an
`AppChannel == 'Prod'` override of `ApplicationId` / `ApplicationPublisher`, or Store-specific
properties passed by the packaging workflow. Record the reserved values here once they exist:

- Reserved Package/Identity/Name: **TODO**
- Reserved Package/Identity/Publisher: **TODO**
- Store ID: **TODO**
- Store product URL (`https://apps.microsoft.com/detail/<StoreId>`): **TODO**

> The product *name* — "GC Toolkit" vs something else — is still an open decision. Reserve the name
> you actually intend to ship under; changing it afterwards means a new identity.

### Secrets — **TODO**

`.github/workflows/package-windows.yml` decodes a signing certificate from
`BASE64_ENCODED_WINDOWS_PFX` (Prod) or `BASE64_ENCODED_WINDOWS_PFX_DEV` (Dev). **No GitHub Actions
secrets are configured on this repository yet**, so packaging jobs will fail at the "Decode the pfx"
step until they are. The full list of secrets each workflow expects is the *Secrets reference* table
in [versioning.md](versioning.md).

For a Windows-only first release you need only the Windows ones — see *Shipping only Windows* below.

---

## 1. Pre-flight

Run the checks locally; CI covers them too, but finding out here is cheaper.

```bash
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj -p:SingleTargetFramework=net10.0
dotnet xstyler -c Settings.XamlStyler -r -d ./src --passive
```

Confirm `version.json` on `main` carries the version you mean to release, and that
`src/GcToolkit/Package.appxmanifest` still has `Identity Version="0.0.0.0"` — `validate-manifest-version.yml`
guards this, because Nerdbank.GitVersioning stamps the real version at build time.

Also confirm the repository is **public**. Every packaging workflow is gated on
`github.event.repository.private == false || github.event_name == 'workflow_dispatch'`, so on a
private repo you must trigger them manually.

## 2. Cut the release branch

**Actions → Prepare Release → Run workflow** (it is `workflow_dispatch` only, and always runs against
`main`). It:

1. reads the current version with `nbgv get-version`,
2. runs `nbgv prepare-release`, which creates `release/v{x.y}` and bumps `main` to the next minor,
3. pushes `release/v{x.y}`,
4. pushes a `chore/bump-version-after-{x.y}` branch and opens a PR titled
   *"chore: bump version after cutting release/v{x.y}"*.

Equivalent locally, if you prefer:

```pwsh
nbgv prepare-release
git push origin main release/v{current}
```

Merge the bump PR so `main` moves on. From here, `release/v{x.y}` is the source of stable builds for
this minor; fixes committed to it bump the patch version and re-run everything.

## 3. Wait for the packaging workflows

Pushing `release/v{x.y}` triggers, in parallel:

| Workflow | Produces | Publishes automatically? |
| --- | --- | --- |
| **Build Windows Packages** (`package-windows.yml`) | x86 / x64 / ARM64 Release MSIX, bundled into a `.msixupload` artifact | No — manual Partner Center upload |
| **Build Android Packages** (`package-android.yml`) | Signed `.aab` (`Android_AAB` artifact) | Yes — Google Play, Production track |
| **Build iOS Packages** (`package-ios.yml`) | Signed `.ipa` (`iOS_IPA` artifact) | Yes — TestFlight upload; "Submit for Review" is manual |
| **Azure Static Web Apps CI/CD** (`static-web-apps-deploy.yml`) | Prod-channel WebAssembly build | Yes — production Static Web App |

All of them compute `AppChannel=Prod` from the `release/v*` ref, which is what gives the package its
Prod identity, the non-Dev icon, and no DEV banner.

**Verify all four are green before continuing.** A failed Android or iOS run does not stop you
shipping Windows, but it does stop the tag — see below.

### Shipping only Windows

Android packaging needs signing secrets that do not exist yet. Rather than blocking a Windows-only
first release, set the repository variable **`REQUIRED_RELEASE_PLATFORMS`** (Settings → Secrets and
variables → Actions → Variables) to e.g. `windows`. `tag-release.yml` reads it and only requires the
listed platforms to be green before tagging. Remove the variable once all three platforms build.

## 4. Get the `.msixupload`

Open the successful **Build Windows Packages** run → **Artifacts** → download
`GcToolkit_<version>.msixupload`. It is a zip containing the `.msixbundle` (x86 + x64 + ARM64) plus
the `.appxsym` symbol files, which is exactly what Partner Center wants.

> **Known quirk:** the `merge` job reads the version for the artifact *name* from the checked-in
> `Package.appxmanifest`, which is pinned to `0.0.0.0` — so the artifact is called
> `GcToolkit_0.0.0.0.msixupload` even though the packages inside carry the real NBGV version. Cosmetic;
> don't let it convince you that you downloaded the wrong build. Check the version inside the bundle
> if you want to be sure.

## 5. Partner Center submission

<https://partner.microsoft.com/dashboard> → Apps and games → the app → **Start new submission**.
Work through the pages in this order; the order matters only in that Packages fails fastest if the
identity is wrong, so upload early if you are unsure.

> The listing **copy** for each market, and an inventory of the non-text artifacts Partner Center
> asks for, live in [`store/`](../store) — paste from there rather than retyping, so a release is a
> diff instead of a rewrite.

### Pricing and availability

- **Price:** Free.
- **Markets:** all markets is fine — the app is offline and ships EN + CS, and nothing in it is
  market-restricted.
- **Visibility → Audience: Private audience** for the first submission. See step 6.
- **Schedule:** unavailable while you are on a private audience; you set it on the public submission.

### Age ratings

Complete the **IARC** questionnaire. Honest answers for GC Toolkit: no violence, no user-generated
content shared between users, no in-app purchases, no data collection, no advertising, no
unrestricted internet access. It should come back as the lowest rating in every region.

### Properties

- **Category:** Utilities & tools. (Consider *Navigation & maps* as a secondary category once the
  field/map tools land.)
- **Privacy policy URL:** required here, because a full-trust desktop package declares
  `runFullTrust`, which makes the Store treat the app as capable of touching personal information —
  regardless of the fact that it doesn't. Point it at [PRIVACY.md](../PRIVACY.md):
  `https://github.com/MartinZikmund/gc-toolbox/blob/main/PRIVACY.md`, or a nicer rendered copy on the
  Static Web App if you prefer. **TODO: pick one and make sure it stays reachable.**
- **Website:** the GitHub repository, or the Static Web App URL.
- **Support contact info:** `martin@zikmund.dev` (or the GitHub issues URL).
- **Product declarations:** nothing exotic applies. The app does not access the internet at runtime.
- **System requirements:** leave at defaults.

### Store listings — one per language

Create **English (United States)** and **Czech (Czech Republic)** listings; those are the two the app
itself ships. Each needs:

- Product name, short description, and description — the README's pitch and tool list is the raw
  material; keep the Czech listing genuinely translated, not machine-shaped.
- **Screenshots:** at least one 1366×768 or larger desktop capture. Take them from the packaged
  Windows build so the frame and theme are real.
- Search terms: the terms cachers actually type (geocaching, puzzle, mystery cache, cipher,
  coordinates, souřadnice, šifry…).
- **Additional license terms** (under *Additional information* on the listing page): GC Toolkit is
  GPLv3, which is *not* the Store's default Standard Application License Terms. Put the GPLv3 notice
  and a link to <https://www.gnu.org/licenses/gpl-3.0.html> here — and note that the app's bundled
  icons are licensed separately (see [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)).

### Packages

Upload the `.msixupload` from step 4. Errors here are almost always the identity mismatch described
in *Store identity* above.

### Submission options

Add **certification notes** telling the tester what the app is: an offline calculator toolkit, no
sign-in, no network access, nothing hidden behind a login. It shortens review.

Then **Submit to the Store**.

## 6. Private audience first, then public

Publish the first submission to a **Private audience** (a known user group containing your own
Microsoft account and any testers). This is the safety net: certification runs for real, the package
installs from the Store exactly as a customer would get it, and nobody else can see the listing —
not even by direct link.

Install it on a clean machine and smoke-test: the gallery loads, search works, favourites persist
across restarts, a handful of tools give correct answers, both languages render, and light/dark
themes look right.

Then create a **new submission** and change Visibility to **Public audience**.

> **This is one-way.** Once a product has been published to a public audience you cannot go back to a
> private audience in a later submission. Do the private pass first, or not at all.

## 7. Tag

`tag-release.yml` runs automatically after the packaging workflows complete on `release/v*`. It
checks that every workflow named in `REQUIRED_RELEASE_PLATFORMS` (default: all three) succeeded on
that exact commit *and* branch, then runs `nbgv tag` and pushes an annotated `v{x.y.z}` tag.

It skips silently while the required workflows are not all green, so check the tag actually landed:

```bash
git fetch --tags && git tag --list "v*" --sort=-v:refname | head -3
```

If it didn't, either fix the red workflow and re-run it, or narrow `REQUIRED_RELEASE_PLATFORMS`, or
tag by hand from the release branch:

```pwsh
nbgv tag
git push origin --tags
```

Then create a GitHub Release from the tag with human-readable notes.

## 8. After the release

- **Watch the Partner Center Health report.** The app has no in-app crash reporting, no telemetry and
  no analytics by design, so the Health report's crash and hang data is the *only* signal that
  something is broken in the wild. Check it daily for the first week, then weekly. (Opt-in
  privacy-first analytics is parked in [`ideas.md`](../ideas.md) if this proves too thin.)
- **Read the reviews.** Same reason — with no telemetry, reviews and issues are the feedback loop.
- **Uncomment the Store badge** in [README.md](../README.md) with the real product URL.
- **Patching:** commit the fix to `release/v{x.y}`. NBGV bumps the patch version, the same workflows
  fire, and you repeat from step 3 with a new `.msixupload`. Cherry-pick or merge the fix to `main`
  if it belongs there too.

## Loose ends to close on the first release

- `src/GcToolkit/Services/Rating/AppRatingService.cs` launches
  `ms-windows-store://review/?ProductId=REPLACE_WITH_STORE_ID`. **Replace the placeholder with the
  real Store ID**, or the in-app "rate this app" prompt opens nothing.
- The Icons8 licensing question in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) needs a
  decision before shipping.
- The provenance of the bundled English word list
  (`src/GcToolkit.Core/Ciphers/PhoneKeypadWords-en.gz`) is unrecorded — see the same file.
