# Changelog

All notable changes to Shift Batch are recorded here.

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
