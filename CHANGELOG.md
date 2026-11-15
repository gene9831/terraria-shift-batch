# Changelog

All notable changes to Shift Batch are recorded here.

## [1.1.0] - 2026-11-15

### Changed

- The minimum purchase/craft setting is now 10; older lower settings migrate to 10.

### Added

- Optional auto-stow, off by default: keep holding Shift and the same mouse button at the same shop/recipe for about 0.3 s after the effective capacity limit.
- Optional built-in limit sound, emitted immediately on reaching capacity so releasing the input can cancel the transfer.
- Vanilla personal-inventory placement with cursor remainders, no world drops or void-vault routing, and no repeated no-progress attempts.
- Effective-full handling for multi-output recipes and repeat rearming for nonstacking crafted items.
- Cancellation for input/target changes, pause, focus loss, blocking UI and world/config lifecycle changes.
- Regression coverage for rendering below 60 fps, native complete/partial/full inventory placement, quantity conservation and pickup callback failures.
- Native crafting eligibility and pending server-request guards prevent transfers during temporarily blocked interactions.

### Verified

- Automated suite: 239 assertions. New auto-stow gameplay and multiplayer verification remain pending.

## [1.0.0] - 2026-11-15

### Added

- Separate F6 settings for Shift buying and crafting batches, both defaulting to 10.
- Integer input validation with a range of 1–9999 and retention of the previous value after invalid input.
- Stack-capacity limits for accelerated buying and multi-output crafting recipes.
- Scoped protection against precision loss while loading raw JSON configuration numbers.
- Windows build, test, and packaging scripts using locally installed game dependencies.
- English and Simplified Chinese documentation.

### Verified

- Automated suite: 119 assertions against Terraria 1.4.5.8 and Core assembly 0.4.2.0.
- Maintainer-reported successful in-game verification on 2026-11-15.

Multiplayer behavior remains unverified.
