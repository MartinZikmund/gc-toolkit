# GC Toolkit

**A free, open-source toolkit for solving geocaching puzzles — native, offline, on every platform.**

GC Toolkit is a gallery of focused solving tools: coordinate maths, classical ciphers, number
sequences, reference alphabets and text utilities. It is aimed at mystery/puzzle cache solvers who
are tired of juggling a dozen browser tabs of one-off calculators — everything is in one app behind
one search box, it works with no connection, and it remembers your favourites and recents.

It is built with the [Uno Platform](https://platform.uno/), so the same app runs on Windows,
Android, iOS, macOS, Linux and in the browser. It is **provider-agnostic**: it talks to no listing
service and makes no network calls at all. See [PRIVACY.md](PRIVACY.md).

<!--
  SCREENSHOT — not committed yet.
  Drop a PNG of the tool gallery (Windows head, light theme, ~1280x800) at
  docs/images/screenshot-gallery.png, then replace this comment with:
      ![The GC Toolkit tool gallery](docs/images/screenshot-gallery.png)
  Keep the file under ~500 KB so cloning stays cheap.
-->

<!--
  MICROSOFT STORE BADGE — the app is not listed yet.
  Once the Store submission is live, replace this comment with:
      [![Get it from Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](STORE_PRODUCT_URL)
  substituting STORE_PRODUCT_URL for the product page URL from Partner Center
  (https://apps.microsoft.com/detail/<StoreId>). See docs/release-runbook.md.
-->

## Tools

43 tools ship today, grouped into five categories. The catalog is discovered at compile time, so it
grows by adding a view model — see [CONTRIBUTING.md](CONTRIBUTING.md).

### Coordinates

| Tool | What it does |
| --- | --- |
| Coordinate conversion | Convert coordinates between formats (WGS84, UTM, …) |
| Distance & bearing | Measure the distance and bearing between two points |
| Waypoint projection | Project a waypoint from a point, bearing and distance |

### Ciphers

| Tool | What it does |
| --- | --- |
| Affine cipher | Encode and decode `E(x) = (a·x + b) mod 26` — or auto-solve an unknown key |
| Atbash cipher | Mirror the alphabet (A↔Z, B↔Y, …) |
| Bacon cipher | The Baconian biliteral cipher — each letter as a five-symbol group |
| Caesar cipher | Caesar shifts including ROT13 — or every shift at once |
| Gronsfeld cipher | A Vigenère variant with a numeric key |
| Kamasutra cipher | Reciprocal letter-pair substitution |
| Kenny code | The South Park "mmm / mpp / mpf" cipher |
| One-time pad | Vernam one-time pad encryption and decryption |
| Phone keypad | Multitap text ↔ keypad digits, with offline vanity-code decoding |
| Rail fence cipher | Zig-zag transposition — draw the fence, or auto-solve the key |
| Scytale cipher | The ancient rod transposition, with every column count at once |
| Substitution cipher | A custom 26-letter alphabet, or one generated from a keyword |
| Trithemius cipher | The keyless progressive cipher (the ancestor of Vigenère) |
| Vigenère cipher | Encode and decode text with a Vigenère key |

### Alphabets

| Tool | What it does |
| --- | --- |
| Braille | Text ↔ Grade-1 Braille (Unicode dots) |
| Brainfuck & Ook! | Run, generate and transliterate Brainfuck and its Ook! variants |
| Colour conversion | Hex, RGB, CMY, CMYK, HSL, HSV, HSI, YIQ, YUV, YCbCr |
| Dvorak keyboard | Re-type text between keyboard layouts by key position |
| GC code ↔ ID | Convert a public GC code to and from its internal numeric ID |
| Morse code | Text ↔ Morse code |
| Resistor colour code | Decode resistor bands to a value, or encode a value to bands |
| Semaphore flags | Text ↔ flag semaphore figures |
| Signal flags | Text ↔ international maritime signal flags (ICS) |
| Spelling alphabet | NATO phonetic code words plus ten historical variants |
| Tap code | Letters ↔ tap numbers and knock patterns on a Polybius square |

### Numbers

| Tool | What it does |
| --- | --- |
| Angle conversion | Degrees, radians, gradians, mils, turns, compass points and more |
| Base converter | Numbers between bases (binary, hex, …) |
| Collatz conjecture | Trace the 3n+1 hailstone sequence, or find the most stubborn starter |
| Euler's number (e) | Look up the decimals of e — 1,000,000 of them, offline |
| Fibonacci numbers | Generate, test membership, and look up by position or value |
| Golden ratio (φ) | Look up the decimals of φ — 1,000,000 of them, offline |
| Kaprekar's constant | Run Kaprekar's routine toward 6174 (or 495), step by step |
| Lucas numbers | Explore the Lucas sequence (2, 1, 3, 4, 7, …) |
| Pi (π) | Look up decimals of π — 1,000,000 of them, offline |
| Prime numbers | Primality, the n-th prime, the nearest prime, factorizations |
| Roman numerals | Roman numerals ↔ numbers, both ways |

### Text

| Tool | What it does |
| --- | --- |
| ASCII converter | Text ↔ character codes (decimal, hex, octal, binary), full Unicode |
| Numbers to letters | Letters ↔ their A1Z26 numbers (A=1 … Z=26) |
| Reverse text | Reverse, shuffle or flip text — by string, line, word or character |
| Word value | Add up character values and reduce the total to its digital root |

## Supported platforms

| Target | Target framework |
| --- | --- |
| Windows (WinAppSDK) | `net10.0-windows10.0.26100` |
| Android | `net10.0-android` |
| iOS | `net10.0-ios` |
| Desktop — Windows, macOS, Linux | `net10.0-desktop` |
| WebAssembly | `net10.0-browserwasm` |

## Build & run

You need the .NET SDK pinned in [`global.json`](global.json) and, per target, that platform's
workload. Restore the local tools once after cloning:

```bash
dotnet tool restore
```

Then build **one** head at a time. `-p:SingleTargetFramework=` collapses the multi-targeted projects
to a single target framework for **restore as well as build**, so only that platform's workload is
needed:

```bash
# Cross-platform desktop (Windows, macOS, Linux)
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-desktop

# Windows (WinAppSDK)
dotnet build src/GcToolkit/GcToolkit.csproj -p:SingleTargetFramework=net10.0-windows10.0.26100
```

> Use `-p:SingleTargetFramework=`, not a bare `-f`. `-f` narrows only the *build*; the implicit
> restore still evaluates every target framework and fails with `NETSDK1147`, demanding the
> android/ios/wasm workloads even for a desktop build.

Run the unit tests (MSTest on the Microsoft Testing Platform):

```bash
dotnet test tests/GcToolkit.Core.Tests/GcToolkit.Core.Tests.csproj -p:SingleTargetFramework=net10.0
```

More detail — repeated local builds, running the WinUI head, XAML formatting, adding a tool — is in
[CONTRIBUTING.md](CONTRIBUTING.md).

## Localization

The app ships in **English** and **Czech**. Every user-facing string lives in
`src/GcToolkit/Strings/<lang>/Resources.resw`, so adding a language is largely a matter of copying
that folder and translating it. Translations and corrections are very welcome — see
[CONTRIBUTING.md](CONTRIBUTING.md).

## Contributing

Bug reports, tool ideas and pull requests are welcome. Start with
[CONTRIBUTING.md](CONTRIBUTING.md); please also read the [Code of Conduct](CODE_OF_CONDUCT.md). To
report a security issue privately, see [SECURITY.md](SECURITY.md).

Deliberately deferred ideas live in [`ideas.md`](ideas.md).

## Versioning

Versions are computed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
from `version.json` and git height — never hand-edited. `main` produces `0.X.0-dev.{height}`
prereleases with a Dev-channel package identity that installs side by side with the Store build;
stable releases come from `release/v{major}.{minor}` branches. See
[docs/versioning.md](docs/versioning.md) for the full model,
[docs/versioning-migration.md](docs/versioning-migration.md) to apply it to an existing app, and
[docs/release-runbook.md](docs/release-runbook.md) for how a release actually ships.

## Licence

GC Toolkit is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for the
full text, and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the licences of the components
and data that ship inside the app.
