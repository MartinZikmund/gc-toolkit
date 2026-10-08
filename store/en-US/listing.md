# Microsoft Store listing — en-US

Draft copy for the Partner Center **Store listing** page, market **United States / English**.
One section per Partner Center field, with its limit in the heading and the current length noted
above each block. Everything inside a fenced block is meant to be pasted verbatim; everything
outside it is a note for whoever is filling the form.

Counts include newlines, which is how Partner Center counts them.

---

## App name — PENDING A BRAND DECISION

> **Do not treat this as settled.** The product name is the one open item in the PRD
> (`docs/PRD.md`, §8 and §11) and it has to be **reserved in Partner Center before the first
> submission**. Nothing in the repo should be renamed until the reservation succeeds — the
> `ApplicationTitle`, `ApplicationId` (`dev.mzikmund.gctoolkit`) and the `ApplicationName`
> resource string are deliberately left alone.

Constraints that narrow the field:

- **"Geocaching" and "Geocaching.com" are registered trademarks of Groundspeak, Inc.** They must
  not appear in the app title, and should not be used in a way that implies affiliation. Using
  "for geocaching" as a plain descriptor inside the description is fine; putting it in the name
  is not.
- **geocachingtoolbox.com is a live product** with a very similar name and overlapping purpose.
  Avoid "toolbox" in the title even though the repository is called `gc-toolbox` — a reviewer or a
  user comparing the two should never have to work out which is which.
- **The reserved name is global.** Partner Center reserves one product name for the whole app;
  the cs-CZ listing below has no separate name field, so whatever is reserved is also what Czech
  users see. Pick something that does not read badly in Czech.

Candidates, in the order I would try to reserve them:

| Candidate | Rationale |
| --- | --- |
| **GC Toolkit** | Status quo: already the `ApplicationTitle`, the `ApplicationId` and the resource string, so shipping it costs nothing. "GC" is the community shorthand cachers actually type, and it avoids the Groundspeak word marks. Weakness: it is close to generic, so it is hard to defend as a mark, and "GC" is also how Groundspeak's own cache codes are prefixed. |
| **Cachewright** | A coined single word — the strongest of the three as a trademark, and unmistakably distinct from geocachingtoolbox.com. "Wright" reads as maker/solver, which fits a puzzle toolkit. Weakness: opaque, so it earns nothing from Store search on its own and leans entirely on the short description. |
| **Puzzle Cacher** | Describes the primary persona in the PRD exactly and matches how people search. Weakness: descriptive names are weak marks and this one is easy for someone else to reserve a near-variant of. |

Whichever wins, reserve the **exact** string first, then update `ApplicationTitle`,
`ApplicationId`, the `ApplicationName` resource in both languages, and this file — in that order.

---

## Short description — 270 character limit

Length: **259**

```text
43 offline tools for solving puzzle and mystery caches: coordinate conversion and projection, classic ciphers, number sequences, and code tables such as Morse, Braille and semaphore. Free, open source, English and Czech. No account, no ads, no network access.
```

---

## Description — 10,000 character limit

Length: **4003**

```text
GC Toolkit is a free, native toolkit for solving puzzle and mystery caches. Everything runs on your device. There is no account, no sign-in, no ads, and no network access at all — the app never opens a connection, so nothing you paste into it leaves your machine. Work out a projection with no signal, decode a cipher on the train, and close the app knowing nothing was uploaded.

Forty-three tools, grouped into five categories.

COORDINATES
- Coordinate conversion — move between WGS84 formats and UTM
- Distance and bearing between two points
- Waypoint projection from a starting point, a bearing and a distance

CIPHERS
Caesar shifts including ROT13, with an all-shifts view so you can read the answer off the list. Vigenere, and its numeric-key variant Gronsfeld. Affine, with automatic recovery of an unknown key. Atbash. Bacon's five-symbol biliteral cipher. A substitution cipher you can drive from a custom 26-letter alphabet or build from a keyword. Kamasutra letter pairs. The Vernam one-time pad. Rail fence and Scytale transpositions, both of which will brute-force an unknown key and show you the candidates. Trithemius, the keyless ancestor of Vigenere. Old-phone multitap on a keypad. And the South Park "mmm / mpp / mpf" Kenny code, because someone will use it eventually.

NUMBERS
Base conversion between binary, octal, decimal and hex. Roman numerals both ways. Angle conversion across degrees, radians, gradians, mils, turns and compass points. Primality tests, the n-th prime, a prime's position, the nearest prime and full factorisations. Fibonacci and Lucas sequences, searchable by position or by value. The Collatz 3n+1 trail, including the most stubborn starting number under a limit. Kaprekar's routine stepping towards 6174. And digit lookup in pi, e and the golden ratio — the first N digits, the digit at a position, a range, or every occurrence of a sequence you are hunting for.

ALPHABETS AND CODE TABLES
Morse. Grade-1 Braille. The NATO spelling alphabet plus ten historical variants, in both directions. Flag semaphore. International maritime signal flags. Tap code on a Polybius square, with the knock pattern drawn out. Resistor colour bands, decoded to a value or encoded from one. Colour conversion across hex, RGB, CMY, CMYK, HSL, HSV, HSI, YIQ, YUV and YCbCr. Re-typing text between QWERTY and Dvorak by key position. Brainfuck and its Ook! variants, which the app will also run for you. And GC code to internal ID and back.

TEXT
Character codes in decimal, hex, octal and binary across the full Unicode range. A1Z26 letters to numbers and back. Word value, with the total reduced to its digital root. Reversing, shuffling and flipping text by string, line, word or character.

FINDING THE RIGHT TOOL
The catalog is searchable and grouped by category, and every tool can be starred. Favourites and recently used tools sit on the home screen, so the four or five tools you actually reach for are one tap away rather than buried in a list of forty-three.

PLATFORMS AND LANGUAGES
Built with the Uno Platform from a single codebase. This Store listing covers Windows 10 version 1809 and later. The interface is fully translated into English and Czech — every label, tooltip and result, not just the menus — and you can switch language in Settings without changing your system language. Light, dark and system themes, keyboard-friendly layouts, and a window that reflows from a narrow pane to a full desktop width.

OPEN SOURCE
GC Toolkit is free software under the GNU General Public License, version 3. The complete source, the issue tracker and the release history are at:

https://github.com/MartinZikmund/gc-toolbox

Bug reports, translations and new tool ideas are welcome there. If a tool you rely on is missing, open an issue — the catalog is designed so that adding one is cheap.

Geocaching and Geocaching.com are trademarks of Groundspeak, Inc. This app is an independent project and is not affiliated with, endorsed by or sponsored by Groundspeak, Inc.
```

> Notes for whoever pastes this:
> - Partner Center's description field renders plain text. The all-caps headings are deliberate —
>   they survive the Store's stripping of markup, where `##` or `**bold**` would not.
> - The tool count (**43**) and the per-category counts must be re-checked before every
>   submission. The catalog is generated at compile time from `[Tool(...)]` attributes in
>   `src/GcToolkit.Core/ViewModels/Tools/`, so `grep -c "\[Tool(" src/GcToolkit.Core/ViewModels/Tools/*.cs`
>   is the source of truth, not this file.
> - "This Store listing covers Windows 10 version 1809 and later" matches
>   `MinVersion="10.0.17763.0"` in `src/GcToolkit/Package.appxmanifest`. If that floor moves, this
>   sentence moves with it.

---

## What's new in this version — 1,500 character limit

Length: **402**

```text
First public release.

43 tools for puzzle and mystery caches, across coordinates, ciphers, numbers, alphabets and text. A searchable catalog with favourites and recently used tools. Full English and Czech translations, switchable in Settings. Light, dark and system themes. Works entirely offline with no account.

Found a problem, or missing a tool? https://github.com/MartinZikmund/gc-toolbox/issues
```

---

## Search terms — up to 7, 30 characters each

Not shown to users; they only feed Store search. Lengths in brackets.

```text
mystery cache solver     [20]
puzzle cache             [12]
coordinate converter     [20]
waypoint projection      [19]
cipher decoder           [14]
caesar cipher            [13]
morse code               [10]
```

> **Decision for the owner:** the single highest-value search term is *geocaching*, and it is
> deliberately **not** in the list above. Using a competitor's or rights-holder's trademark as a
> hidden keyword is a grey area: Microsoft's certification policy (10.1/10.8) bars listings that
> misrepresent an association, and Groundspeak is an active enforcer of its marks. Nominative
> descriptive use ("tools for geocaching") in the description body is the safer place for the word,
> and it already appears there. If you decide the risk is acceptable, "geocaching tools" (16) would
> replace "cipher decoder" — but make that call knowingly.

---

## Category and subcategory

**Recommended: Utilities & tools.**

The app is a collection of converters, calculators and lookup tables. That is squarely a utility,
and it is the category where Store users browsing for converters and calculators will look.
Partner Center offers no subcategory under Utilities & tools, so leave that dropdown as-is if it
appears empty.

Runners-up, and why not:

- **Navigation & maps** — tempting because of the coordinate tools, but the field/sensor tools
  (compass, live navigation) are **not** in this release. Filing under Navigation & maps would set
  an expectation the app does not meet, and would invite one-star reviews from people who wanted a
  navigator. Revisit if and when field tools ship.
- **Education** — the cipher and number-sequence tools have obvious teaching value, but the
  audience this is built for is not searching in Education.

---

## Additional license terms

Partner Center's "Additional license terms" field on the Properties page. Paste as plain text.

```text
GC Toolkit is free software. This application is distributed under the terms of the GNU General Public License, version 3 or (at your option) any later version, as published by the Free Software Foundation.

The GPL grant is what governs your use, modification and redistribution of this application. To the extent the Microsoft Standard Application License Terms conflict with the GNU General Public License, the GNU General Public License applies.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

The complete corresponding source code is available at https://github.com/MartinZikmund/gc-toolbox and a copy of the license is included with the source and in the application's About page.

Third-party components are distributed under their own licenses; see the NOTICE and license files in the source repository.
```

> Two things to verify before submitting:
> - Whether the app's **About** page actually surfaces the license and a link to the source. If it
>   does not, either add it or drop that half-sentence — do not claim it.
> - Whether a `NOTICE` / third-party licenses file exists in the repo. If not, drop the last
>   paragraph rather than pointing at something that isn't there.

---

## Copyright and trademark info

Partner Center field, 200 character limit.

Length: **133**

```text
© 2026 Martin Zikmund. Licensed under the GNU General Public License v3.0. Geocaching® is a registered trademark of Groundspeak, Inc.
```

> The fuller disclaimer ("not affiliated with, endorsed by or sponsored by Groundspeak, Inc.")
> does not fit in 200 characters alongside the copyright line, so it lives at the end of the
> Description instead. Keep it there.

---

## URLs and contact

| Field | Value | Status |
| --- | --- | --- |
| Privacy policy URL | **TODO** | **Required.** The app collects nothing and makes no network calls, but Partner Center still demands a reachable URL. Simplest option: a `PRIVACY.md` in the repo published via GitHub Pages, or a plain `docs/privacy.md` linked from `https://github.com/MartinZikmund/gc-toolbox/blob/main/PRIVACY.md`. A raw GitHub file URL is accepted but looks unfinished. |
| Website | `https://github.com/MartinZikmund/gc-toolbox` | Optional; the repo is a reasonable stand-in until there is a product page. |
| Support contact info | **TODO** | `https://github.com/MartinZikmund/gc-toolbox/issues` is the honest answer and is accepted. If you would rather not require a GitHub account of users, use `martin@zikmund.dev` instead — but then be prepared to answer it. |

---

## Fields intentionally left empty

- **Short title, Sort title, Voice title** — Windows-tile and Cortana-era fields; nothing here
  needs them.
- **Device family availability** — not a listing field; see `store/submission-checklist.md`.
- **Trailers and 360° images** — none for the first release.
