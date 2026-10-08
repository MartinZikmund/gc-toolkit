# Third-party notices

GC Toolkit is licensed under the [GNU General Public License v3.0](LICENSE). The app package also
contains third-party software and data, listed here with the licence each one is distributed under.

Licences below were read from the `.nuspec` of the package version this repository actually restores
(or from the licence file the package carries) — not from memory. Versions are governed by
`src/Directory.Packages.props` and by the `Uno.Sdk` version pinned in `global.json`, so they move;
the licences are what matters here. Anything marked **verify** could not be established from the
package metadata and needs a human check before the app ships.

> **Uno Platform packages are Apache-2.0.** Section 4(d) of Apache-2.0 requires that a `NOTICE` file
> distributed with the work be propagated. If any Uno package ships a `NOTICE`, its contents belong
> in this file — **verify** at the version being shipped.

## Direct dependencies

Referenced explicitly from `src/GcToolkit/GcToolkit.csproj` and/or
`src/GcToolkit.Core/GcToolkit.Core.csproj`.

| Component | Licence | Project |
| --- | --- | --- |
| MZikmund.Toolkit.WinUI | MIT (© 2023 Martin Zikmund) | Maintained by this project's author |
| CommunityToolkit.WinUI.Controls.SettingsControls | MIT | https://github.com/CommunityToolkit/Windows |
| CommunityToolkit.WinUI.Converters | MIT | https://github.com/CommunityToolkit/Windows |
| sqlite-net-e | MIT (© Krueger Systems, Inc.) | https://github.com/praeclarum/sqlite-net |
| SourceGear.sqlite3 | SQLite itself is **public domain**; the package carries SourceGear's build (© 2014-2026 SourceGear, LLC) | https://sqlite.org/copyright.html |

## Framework and transitive dependencies

Pulled in by `Uno.Sdk` and by the `UnoFeatures` enabled in `src/Directory.Build.props`
(SkiaRenderer, Hosting, Logging, Mvvm, Configuration, HttpRefit, Serialization, Localization). This
is the shipped set, not the build-only set.

| Component | Licence | Notes |
| --- | --- | --- |
| Uno.WinUI, Uno.Sdk, Uno.WinUI.Graphics2DSK, Uno.Foundation.Logging, Uno.Resizetizer, Uno.UI.Adapter.Microsoft.Extensions.Logging | Apache-2.0 | © Uno Platform Inc. |
| Uno.Extensions.* (Core, Hosting, Configuration, Localization, Logging, Serialization, Storage, Http, Http.Refit) | Apache-2.0 | © Uno Platform Inc. |
| Uno.Toolkit, Uno.Toolkit.WinUI, Uno.Themes.WinUI | Apache-2.0 | Transitive; see "Tech choices to avoid" in CONTRIBUTING.md — we don't take new direct dependencies on these |
| Uno.Core.Extensions.* (Collections, Disposables, Equality, Logging, Logging.Singleton) | Apache-2.0 | © nventive / Uno Platform |
| Uno.Fonts.Roboto | **verify** — the package `.nuspec` declares no licence. Roboto upstream (Google Fonts) is Apache-2.0 | Arrives via Uno.Themes.WinUI |
| CommonServiceLocator | **MS-PL** | Arrives via Uno.Core.Extensions.Logging.Singleton. **Verify** — MS-PL is a free licence but the FSF considers it GPL-incompatible; confirm this is acceptable for a GPLv3 distribution, or confirm the assembly is not actually shipped |
| CommunityToolkit.Mvvm, CommunityToolkit.Common, CommunityToolkit.WinUI.* (Extensions, Helpers, Triggers) | MIT | © .NET Foundation and Contributors |
| Microsoft.Extensions.* (DependencyInjection, Hosting, Logging, Configuration, Options, Localization.Abstractions, Http, Primitives, …) | MIT | © Microsoft / .NET Foundation |
| System.Diagnostics.EventLog, System.Net.Http.WinHttpHandler, Microsoft.Bcl.AsyncInterfaces | MIT | © Microsoft / .NET Foundation |
| Refit, Refit.HttpClientFactory | MIT | Pulled in by the `HttpRefit` Uno feature. The app makes no network calls — see PRIVACY.md |
| SkiaSharp, SkiaSharp.Views.WinUI, SkiaSharp.NativeAssets.* | MIT | © Microsoft. Bundles Google's Skia (BSD-3-Clause) — **verify** that Skia's own notice is propagated |
| SQLitePCLRaw.core, SQLitePCLRaw.provider.e_sqlite3, SQLitePCLRaw.config.e_sqlite3 | Apache-2.0 | Bundled SQLite engine is public domain |
| Microsoft.WindowsAppSDK | **Proprietary — Microsoft Software License Terms** (see the `license.txt` in the package) | Windows head only. **Verify** the redistribution terms are satisfied by a GPLv3 app; this is normally covered by the GPL's system-library exception, but it is a judgement call worth recording |
| Microsoft.Web.WebView2 | Proprietary — Microsoft licence file | Transitive via Microsoft.WindowsAppSDK |

## Build-time only (not shipped in the app package)

| Component | Licence |
| --- | --- |
| Nerdbank.GitVersioning | MIT |
| Microsoft.CodeAnalysis.CSharp, Microsoft.CodeAnalysis.Analyzers | MIT |
| Microsoft.Windows.SDK.BuildTools, Microsoft.Windows.SDK.BuildTools.WinApp | Proprietary — Microsoft licence file |
| XamlStyler.Console (local dotnet tool) | MIT |
| MSTest.Sdk, MSTest.TestAdapter, MSTest.TestFramework, Microsoft.Testing.* | MIT (test projects only) |

---

# Data & assets

Some of what ships inside the app is data or artwork rather than code. Provenance is recorded here as
honestly as it can be; where it is uncertain, that is stated rather than guessed.

## Tool and category icons — **owner action required**

Every PNG under `src/GcToolkit/Assets/Icons/Tools/` and `src/GcToolkit/Assets/Icons/Categories/`
comes from **[Icons8](https://icons8.com)**,
Fluency style, colorful — this is the sourcing rule the project's own tooling documents
(`.claude/skills/building-gc-tools/SKILL.md`).

Icons8's **free** use requires a visible link-back credit wherever the icons appear; without that
credit a paid Icons8 licence is required. The credit line is:

> Icons by [Icons8](https://icons8.com)

**This needs the owner's decision before the Store release**: either (a) surface that credit in the
app (an About/Settings line) and here, or (b) buy the appropriate Icons8 licence and record the
licence/order reference in this file. Note also that the app's own GPLv3 licence does not cover these
icons — they are licensed from Icons8 under Icons8's terms, and that should be stated wherever the
icons' licensing is described.

The app icon itself (`src/GcToolkit/Assets/Icons/icon*.svg`) and the splash screen are project
artwork, covered by this repository's licence.

## `src/GcToolkit.Core/Ciphers/PhoneKeypadWords-en.gz` — **PROVENANCE UNKNOWN, owner action required**

A gzipped, frequency-ranked English word list (~315,000 words, ~960 KB compressed) embedded in the
assembly so the phone-keypad tool can decode vanity codes offline. It was added in commit `b55b5ca`
("feat(phone-keypad): decode vanity codes against a bundled offline word list"), which does **not**
name where the list came from, and no source is recorded in the code or anywhere else in the
repository.

Word lists are frequently redistributable, but their licences vary a great deal — some carry
attribution or share-alike terms, some are derived from corpora with their own conditions.

**Do not ship this file until its origin and licence are established.** Either identify the source
and record it here, or regenerate the list from a source with a known, compatible licence (for
example a public-domain or permissively licensed frequency list) and note that here instead.

## Embedded digits of mathematical constants

The digits themselves are facts about mathematics, not authored works, so there is nothing to
license. Recorded here for traceability, quoting what the code says about how they were produced:

| File | Contents | Stated provenance |
| --- | --- | --- |
| `src/GcToolkit.Core/Numbers/Pi/PiDecimals.txt` | 1,000,000 decimals of π | "verified against three independent sources: a Chudnovsky computation, angio.net and Princeton's `pi-10million.txt`" (`PiDigitsProvider.cs`) |
| `src/GcToolkit.Core/Numbers/GoldenRatio/GoldenRatioDigits.gz` | 1,000,000 decimals of φ | "generated offline as `isqrt(5·10^(2·(1,000,000+100)))` via two independent algorithms (CPython `math.isqrt` and libmpdec `Decimal.sqrt`)" (`GoldenRatioDigitsProvider.cs`) |
| `src/GcToolkit.Core/Numbers/EulerNumberDecimals.gz` | 1,000,000 decimals of e | Computed offline; no source cited in the code beyond the expansion itself |

## Reference data tables

Transcribed by hand from published standards and reference material. Facts and short standardised
tables of this kind are not themselves copyrightable, but the sources each file names are reproduced
here so the transcription can be re-checked:

| Data | Source, as stated in the code |
| --- | --- |
| Morse code (`Alphabets/MorseCodec.cs`) | The full **ITU-R M.1677-1** set — letters, digits, punctuation — plus the accented Latin letters that have their own ITU code |
| Spelling alphabets (`Alphabets/SpellingAlphabets.cs`) | Transcribed "from the authoritative Wikipedia spelling-alphabet tables"; the NATO/ICAO set is exact, and ICAO digit code words are included |
| Resistor colour code (`Alphabets/ResistorCode.cs`) | **IEC 60062** electronic colour code, "verified against Wikipedia's *Electronic color code*" |
| Flag semaphore (`Alphabets/SemaphoreAlphabet.cs`) | Arm positions from the signaller's own left/right, "cross-checked against the standard chart (Wikipedia *Flag semaphore*)" |
| Maritime signal flags (`Alphabets/SignalFlagsAlphabet.cs`) | Geometry "transcribed from the public-domain ICS flag renderings"; the flags are drawn by the app as vector geometry, not bundled as images |
| Braille (`Alphabets/BrailleCodec.cs`) | Grade-1 (uncontracted) English Braille, rendered as Unicode braille patterns (U+2800 block); dot-number lists are the source of truth and cells are derived from them |
| Dvorak / QWERTY layouts (`Alphabets/DvorakCodec.cs`) | "sourced from the official Wikipedia keyboard diagrams and transcribed key-by-key" |
| Coordinate grids (`Coordinates/BritishGrid.cs`, `Coordinates/DutchRd.cs`) | Ordnance Survey, *A guide to coordinate systems in Great Britain*; `github.com/chrisveness/geodesy`; `github.com/djvanderlaan/rijksdriehoek` — algorithms reimplemented, not code copied. **Verify** the reimplementation claim if either upstream is copyleft |

Text quoted from Wikipedia in comments is descriptive attribution of where a table was checked; no
Wikipedia prose is reproduced in the app.

---

Spotted something wrong or missing here? Please open an issue at
<https://github.com/MartinZikmund/gc-toolbox> or email martin@zikmund.dev.
