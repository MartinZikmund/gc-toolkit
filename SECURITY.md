# Security Policy

## Reporting a vulnerability

**Please don't open a public GitHub issue for a security problem.** Use one of these instead:

1. **GitHub private security advisories** (preferred) — go to
   <https://github.com/MartinZikmund/gc-toolbox/security/advisories/new> and file a draft advisory.
   It stays private between you and the maintainer, and it gives us a place to coordinate a fix and,
   if warranted, a CVE.
2. **Email** — <martin@zikmund.dev>. Mention "GC Toolkit security" in the subject.

Useful things to include: what you found, which platform head and app version you saw it on (the
version is shown in the app's settings), the steps to reproduce, and what an attacker could actually
achieve. A proof of concept helps a lot. If you'd like credit in the fix's release notes, say so.

## What to expect

This is a spare-time, single-maintainer project, so there is no SLA and no bug bounty. What is
promised is simple and realistic:

- An acknowledgement that your report was received and read, normally within a few days.
- An honest assessment of whether it is a real issue and roughly when it can be fixed.
- Credit in the release notes if you want it, and a heads-up before anything is made public.

Please give the fix a reasonable window before disclosing publicly, and get in touch if you haven't
heard back — a missed notification is far more likely than a deliberate silence.

## Scope, and why it is small

GC Toolkit is an offline calculator. That shapes the realistic attack surface:

- The app **makes no network calls** — there is no backend, no API, no telemetry and no analytics.
- There are **no accounts and no credentials**, so nothing to steal or replay.
- Everything the app stores — favourites, recents, settings, language and theme — lives **only on the
  device**, in the OS-provided per-app storage.
- The only outbound action is opening a URL the user explicitly tapped (the Store review page, a link
  in the app).
- Puzzle input never leaves the device; it is processed in memory by the tool you are using.

So the interesting classes of issue are things like: input that crashes or hangs the app (a
pathological cipher/regex/number input), a tool that writes outside its own storage, an incorrect
result with a security consequence, a packaging/signing/update problem, or a supply-chain issue in a
dependency we ship.

**Out of scope:** vulnerabilities in the underlying platforms (Windows, Android, iOS, browsers) or in
upstream dependencies — please report those to their maintainers; we will pick up the fixed version.
Reports that consist only of automated-scanner output, with no demonstrated impact on this app, are
also out of scope.

## Supported versions

Only the latest released version is supported. Fixes ship in a new release rather than as patches to
older ones.
