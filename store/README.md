# Store listing source

This folder is the **source of truth for what goes into Microsoft Partner Center**. Every text
field of the Store listing lives here in Markdown, per market, alongside the images that go with
it.

The point is that a release becomes a **diff** instead of a rewrite. Partner Center gives you no
history, no review, and no way to see what changed between submissions — so we keep the copy in
git, edit it here, and paste it there. Before a submission, `git diff` against the previous
release tag tells you exactly which fields you have to touch in the form.

**Nothing in this folder ships in the app package.** It is documentation and marketing assets, not
build inputs.

## Layout

```
store/
  README.md                     this file
  submission-checklist.md       the non-text artifacts Partner Center wants
  en-US/
    listing.md                  every en-US text field, with limits and character counts
    screenshots/                en-US screenshots, in the order they should appear
  cs-CZ/
    listing.md                  every cs-CZ text field
    screenshots/                cs-CZ screenshots, in the same order
```

The app ships English and Czech (`LocalizationConfiguration:Cultures` in
`src/GcToolkit/appsettings.json`), so both markets get a full listing with screenshots in the
matching language. A Czech user seeing English screenshots is the fastest way to look like a
machine-translated listing.

> **These files are tracked.** `.gitignore` line 505 ignores `.screenshots/` — the leading dot
> matters. That is the scratch folder `winapp ui screenshot` writes into during development, and
> it is a *different* folder. `store/en-US/screenshots/` and `store/cs-CZ/screenshots/` are not
> ignored and are meant to be committed. Verify with `git check-ignore -v store/en-US/screenshots`
> if you are ever unsure.

## Image assets and their exact dimensions

| Asset | Exact size | Format | Required? | Notes |
| --- | --- | --- | --- | --- |
| Desktop screenshot | **1366 × 768 px minimum**, **2560 × 1440 px preferred** (16:9) | PNG | **Yes** — at least 1 | Up to 10 per language. Partner Center rejects anything under 1366 × 768 or over 3840 × 2160. Ship them all at one size; a mix of aspect ratios looks careless in the Store's carousel. |
| Store logo | **300 × 300 px** | PNG | Recommended | Shown in Store search results and in some placements instead of the package logo. Without it the Store falls back to a scaled package tile, which usually looks soft. |
| Hero image | **2400 × 1200 px** | PNG | Optional | Only used if Microsoft features the app. Cheap insurance — an app with no hero image cannot be featured in the placements that need one. Keep text out of the middle third; that is where the Store overlays the title. |

Name files so the sort order matches the display order — `01-catalog.png`, `02-coordinates.png`,
and so on. Partner Center orders screenshots by upload, and re-ordering them in the form is
tedious.

## Capturing the screenshots

1. **Build the Prod channel.** The Dev channel stamps a distinct "dev" icon and identity
   (`src/GcToolkit/GcToolkit.csproj`, the `AppChannel` property groups). Screenshots must come
   from a Prod build or the badge ends up in the Store:

   ```powershell
   dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-windows10.0.26100 -c Release -p:AppChannel=Prod
   ```

2. **Set the app language to match the folder** — Settings → language → English for `en-US`,
   Czech for `cs-CZ`. The switch needs an app restart to take effect. Do a full pass in one
   language, then switch and do the whole pass again; alternating is how you end up with one
   English screenshot in the Czech set.

3. **Set the window to a clean, consistent size** and use the same one for every shot in both
   languages. A 16:9 window that captures at 2560 × 1440 saves you a resize pass later.

4. **Capture this set**, in this order — it walks a new user from "what is this" to "I can find
   my way around":

   | # | Screen | Why |
   | --- | --- | --- |
   | 1 | **Catalog** | The one shot that has to sell the app: the whole categorised tool list. |
   | 2 | **A coordinate tool** — waypoint projection, with real values filled in | Speaks directly to the primary audience. Empty inputs sell nothing; fill them. |
   | 3 | **A cipher tool** — Caesar with the all-shifts view, or Vigenère | Shows the app doing the work rather than just offering a text box. |
   | 4 | **A code-table tool** — Morse, semaphore or signal flags | The visual ones. Pick whichever renders best at the capture size. |
   | 5 | **Search** | Shows the catalog is navigable, not a wall of forty-three tiles. |
   | 6 | **Settings** | Theme and language, which is where the Czech listing earns its keep. |

5. **Use realistic content.** Real coordinates, a real ciphertext, a plausible result. Placeholder
   text in a screenshot reads as an unfinished app.

6. **Check both themes.** Capture the set in whichever theme looks better and stay in it — a
   carousel that flips between light and dark looks like a mistake rather than a feature. If you
   want to show the other theme, make the Settings shot the one that does it.

7. **Copy the finished PNGs** into `store/en-US/screenshots/` or `store/cs-CZ/screenshots/` and
   commit them. If you captured via `winapp ui screenshot`, they landed in the git-ignored
   `.screenshots/` folder at the repo root — move them, do not link to them.

The build/launch/screenshot mechanics for the Windows head are in
[`.claude/skills/run-winui-app/SKILL.md`](../.claude/skills/run-winui-app/SKILL.md).

## Keeping this folder honest

- The listings quote a **tool count (43)** and per-category contents. The catalog is generated at
  compile time from `[Tool(...)]` attributes, so re-check with
  `grep -c "\[Tool(" src/GcToolkit.Core/ViewModels/Tools/*.cs` before every submission.
- The Czech listing deliberately reuses the terminology from
  `src/GcToolkit/Strings/cs/Resources.resw`. If a translation changes in the app, change it here
  too — a listing that names tools differently from the app is worse than no listing.
- Re-shoot the screenshots whenever the shell or a featured tool visibly changes. Stale
  screenshots are the most common reason a good app looks abandoned.
