# Post-MVP Ideas Backlog

This file captures ideas, alternatives, and features that were deliberately deferred out of
the MVP during PRD brainstorming. Nothing here is committed scope — it's a parking lot of
things worth revisiting after the MVP ships. Each entry notes where it came from.

> Maintained during PRD interview (started 2026-05-27). Newest sections appended as decisions are made.

## Integrations

- **Live listing-service integration** (from Q1): Deep integration with geocaching.com Live API
  (and/or Opencaching) — pull caches, descriptions, logs, and the user's finds directly into the
  app. Requires Groundspeak Partner program approval, API quotas/ToS compliance, and a per-provider
  abstraction. Explicitly a post-MVP release.

## Solving experience

- **Per-cache "solver workspace"** (from Q3): Each mystery/puzzle cache gets a saved project —
  puzzle text & photos, scratch notes, named variables, the ability to run any tool *inside* the
  project and pipe its result into a variable, saved candidate coordinates, and a "solved" state.
  This is the main differentiator over a website of one-off calculators and the natural home for
  the future AI helper. Deferred in favor of standalone tools for the MVP.
- **Lightweight per-cache scratchpad** (from Q3, intermediate option): A simpler middle step toward
  the full workspace — free-text notes plus a saved list of coordinates/waypoints per cache, without
  structured variables or tool-result piping. A possible stepping stone before the full workspace.

## AI solving helper (headline future vision)

- **AI solving assistant** (from Q9): A post-MVP assistant with specialized "skills" that (a) identifies
  the likely puzzle/cipher type from a cache's description and (b) guides the user. Default mode is
  graduated, escalating hints that preserve the spirit of the game; an explicit opt-in "solve it fully"
  mode can propose a complete solution / final coordinates when the user insists. Calls the app's tools
  programmatically via the shared tool contract. Needs a backend + model (e.g. Claude) and a
  monetization answer to cover model cost. Note etiquette/community and listing-service ToS concerns
  around auto-solving and posting solutions. MVP only leaves the AI-invokable tool-contract hooks.

## Analytics

- **Opt-in privacy-first analytics** (from Q15): Add anonymous, no-PII usage analytics (e.g. Aptabase,
  EU-hostable) behind explicit opt-in to measure retention and which tools matter. MVP relies on
  app-store dashboards + ratings only.

## Monetization (future)

- **AI cost recovery** (from Q9/Q10): The MVP is free + open source with a supporter/tip option only.
  When the AI helper lands, decide how to cover model cost — e.g. an optional subscription, a credit
  pack, or bring-your-own-API-key. Do not gate the existing free toolkit behind it.

## Accounts & sync

- **Accounts + cross-device cloud sync** (from Q8): Sign-in and a backend that syncs favorites,
  saved coordinates, history, and settings across the user's devices. Deferred; pairs with the AI
  and listing-service work, which need a backend regardless. MVP is local-first only.

## Import / export

- **GPX import** (from Q13): Import GPX files (Groundspeak + generic) into the local list of saved
  caches/waypoints, usable by coordinate tools and navigation. MVP is manual-entry + paste only.
- **Rich metadata import** (from Q13): Parse full cache metadata (descriptions, hints, attributes,
  logs) — most useful once the solver workspace exists to hold it.
- **Export / share to GPX or geo: links** (from Q13): Export a coordinate or tool result as GPX or a
  shareable geo: link. MVP may still include basic OS "share text" of a coordinate; richer export deferred.

## Field & maps

- **Offline map tiles / downloadable regions** (from Q7): Let users download map areas for offline
  field use (e.g. MBTiles via Mapsui). MVP uses online tiles only. Pairs with offline navigation in
  areas with no signal.

## Cut from the first release

Tools that existed as stubs but were removed before v1 rather than shipped half-done. Nothing here
is hard — each is a small, self-contained piece of work once its prerequisite exists.

- **Compass** (cut 2026-08-19): A live magnetometer heading with a rotating rose, and eventually a
  bearing-to-target arrow. Needs a sensor abstraction that Core can depend on — an `ICompassService`
  in Core with per-head implementations (Windows `Magnetometer`/`Compass`, Android `SensorManager`,
  iOS `CLLocationManager`), plus a graceful "no magnetometer" state for desktop and WebAssembly.
  Pairs with the field/navigation tools already parked under *Field & maps*.
- **Flashlight** (cut 2026-08-19): Torch on/off for reading a cache log in the dark, falling back to
  a full-brightness white screen where there is no torch. Needs a torch/screen-brightness abstraction
  per head (Android `CameraManager.setTorchMode`, iOS `AVCaptureDevice.TorchMode`) and a
  keep-screen-on request; there is no sensible Windows or WebAssembly implementation, so it is a
  mobile-only tool and the gallery would need to hide it elsewhere.
- **Checksum / hash** (cut 2026-08-19): Hex digests (MD5, SHA-1, SHA-256, CRC32) of text — pure Core
  logic with no platform dependency, so it is the cheapest of the three to bring back. Note that the
  *numeric* checksums cachers actually use most — cross-sum and digital root — already ship inside
  the **Word value** tool, so a revived tool should be framed as cryptographic hashes rather than as
  a "checksum" that duplicates what is there.
