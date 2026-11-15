# Changelog

Releases are built, validated and published automatically by GitHub Actions from maintainer-approved version tags.

## [1.0.0] - 2026-11-15

### Added

- Separate buying/crafting settings on the mod configuration page, defaulting to 10 with validated integer input 10–9999.
- Native stack-capacity limits, including accelerated purchases, finite stock and multi-output crafting recipes.
- Optional auto-stow, off by default: keep holding Shift and the same mouse button over the same valid shop item or recipe for 18 simulation ticks after effective capacity is reached.
- Personal-inventory placement with quantity conservation, partial cursor remainders, no world drops/void-vault routing, and no repeated no-progress attempts.
- Cancellation for input/target changes, pause, focus loss, blocking UI, pending native requests and world/config lifecycle changes.
- Optional DebugLogging switch, off by default and applied live. Logs settings, runtime/build identity, patch loading and auto-stow transitions without per-frame waiting/full-bag spam. Only this mod's logger threshold is changed.
- English/Chinese source documentation and Windows build/test/package scripts using existing local dependencies.

### Fixed during development

- Shop input observation occurs before the native full-cursor early return; inactive button callbacks do not reset the held button's delay.
- Raw JSON floating-point values cannot become valid integer settings through Core coercion or precision loss.
- Pickup callback failures preserve inventory/cursor quantity rather than duplicating an already placed stack.

### Packaging

- Installation ZIPs contain only ShiftBatch.dll, manifest.json and LICENSE.txt. README files stay in the repository/source ZIP.
- Rebuilt staging prevents stale documentation/binaries from leaking into runtime archives. Archive names follow the manifest version.
- The prototype capacity sound and its setting were removed. Config migration preserves existing batch and AutoStow settings; new diagnostics off by default.

### Verification

- Automated suite: 272 assertions against Terraria 1.4.5.8, Core assembly 0.4.2.0 and Harmony2.3.3.
- Separate real packaging regression checks exact runtime contents, stale-file exclusion, retained source documentation and version consistency.
- Maintainer verified buying, crafting and corrected auto-stow in game on 2026-11-15. Debug logging and live configuration were also verified in game. Broader multiplayer compatibility remains unverified.
### CI and publication

- GitHub-hosted Windows builds obtain checksum-pinned official public dependencies; no local runner or private dependency upload is required.
- Main/PR checks and tag-only publication enforce version consistency, full headless tests, package whitelists, client references and SHA-256.
- CI publishes installation/source ZIPs and SHA256SUMS.txt from the exact successful build artifact.
