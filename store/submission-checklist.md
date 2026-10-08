# Submission checklist — artifact inventory

Everything Partner Center asks for that is **not** listing copy. Tick a line only when the thing
actually exists and has been entered.

This is an **inventory**, not a process. The order of operations for cutting a release — branch,
version, build, sign, package, upload, publish — lives in
[`docs/release-runbook.md`](../docs/release-runbook.md). Follow the runbook; use this list to
confirm nothing is missing before you start it.

The copy for every text field is in [`en-US/listing.md`](en-US/listing.md) and
[`cs-CZ/listing.md`](cs-CZ/listing.md).

---

## Blocking — the submission cannot be created without these

- [ ] **Reserved app name.** The brand decision is still open (`docs/PRD.md` §8, §11). Reserve the
      exact string in Partner Center **before** renaming anything in the repo. Candidates and the
      trademark constraints are in [`en-US/listing.md`](en-US/listing.md#app-name--pending-a-brand-decision).
- [ ] **Publisher display name** matches what is in the package (`ApplicationPublisher` is
      `Martin Zikmund` in `src/GcToolkit/GcToolkit.csproj`).
- [ ] **Package uploaded** and free of certification errors. Built from the `Prod` channel
      (`-p:AppChannel=Prod`) so the identity is `dev.mzikmund.gctoolkit`, not the `.dev` one.
- [ ] **Privacy policy URL** — required even though the app collects nothing and makes no network
      calls. Must be publicly reachable; a 404 fails certification. Currently **TODO**.
- [ ] **Age rating / IARC questionnaire** completed. Expect the lowest rating: no user-generated
      content, no ads, no purchases, no data collection, no communication features. Answer
      honestly rather than optimistically — a wrong answer here is a re-submission.
- [ ] **At least one screenshot per listed language**, meeting the size rules in
      [`README.md`](README.md#image-assets-and-their-exact-dimensions).

## Product setup

- [ ] **Pricing: Free.** No trial, no in-app purchases. GPLv3 plus the PRD's "free on all stores"
      constraint means this is not a decision to revisit casually.
- [ ] **Markets: all**, unless there is a reason to exclude one. English and Czech listings cover
      the two languages we can support; every other market falls back to the en-US listing, which
      is fine.
- [ ] **Device families.** Windows 10/11 **Desktop** only for this release. The manifest declares
      `Windows.Universal` and `Windows.Desktop` with `MinVersion 10.0.17763.0`
      (`src/GcToolkit/Package.appxmanifest`) — if you narrow that, narrow the "Windows 10 version
      1809 and later" sentence in both listings to match.
- [ ] **Category:** Utilities & tools. Reasoning, and why not Navigation & maps, is in
      [`en-US/listing.md`](en-US/listing.md#category-and-subcategory).
- [ ] **Visibility / discoverability:** public, available for anyone to find. (Hidden or
      link-only is worth considering for a first-run sanity check, but decide it deliberately.)
- [ ] **Publish schedule:** manual, so the release goes live when you say so rather than the
      moment certification passes.

## Legal and contact

- [ ] **Additional license terms** — the GPLv3 statement from
      [`en-US/listing.md`](en-US/listing.md#additional-license-terms). Do not leave this blank:
      the Microsoft Standard Application License Terms are the default, and they conflict with
      the GPL.
- [ ] **Copyright and trademark info** filled in for **both** listings (it is a per-language
      field).
- [ ] **Support contact** — a URL or an email that someone will actually read. Currently **TODO**.
- [ ] **Groundspeak disclaimer** present at the end of both descriptions. Verify it survived any
      last-minute trimming of the description.
- [ ] **`LICENSE` (GPLv3) present in the repo** and reachable from the URL quoted in the licence
      terms.

## Images

- [ ] Screenshots, **en-US** — catalog, a coordinate tool, a cipher tool, a code-table tool,
      search, settings. Captured from a `Prod` build with the app language set to English.
- [ ] Screenshots, **cs-CZ** — the same six screens, app language set to Czech.
- [ ] **Store logo, 300 × 300 px.**
- [ ] **Hero image, 2400 × 1200 px** — optional, but the app cannot be featured without it.
- [ ] All images committed under `store/<locale>/screenshots/` so the next release starts from
      what shipped rather than from scratch.

## Final sanity pass

- [ ] **Tool count is right.** Both listings claim 43 tools across five categories. Verify with
      `grep -c "\[Tool(" src/GcToolkit.Core/ViewModels/Tools/*.cs` — the catalog is generated
      from those attributes at compile time, so the code is the source of truth, not the copy.
- [ ] **No tool is described that is not in the build**, and nothing shipped is left undescribed.
- [ ] **The installed Prod build was actually run** in both languages, and the screens in the
      screenshots match what a user gets.
- [ ] **Version in the package** is what the runbook expects (Nerdbank.GitVersioning computes it;
      see [`docs/versioning.md`](../docs/versioning.md)). Never hand-edit it in the manifest.
