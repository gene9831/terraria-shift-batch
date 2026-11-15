# Development notes

## Structure

```text
src/
  AssemblyInfo.cs      Assembly version and net48 target metadata
  BatchRules.cs        Integer normalization and stack-capacity arithmetic
  BatchConfig.cs       Separate F6 settings and validation warnings
  ConfigInputGuard.cs  Scoped protection before Core numeric coercion
  NativePatches.cs     Native quantity hooks and IL transformations
  Mod.cs               Initialization, patch lifecycle, and warning display
tests/Tests.cs         Executable assertions against local dependencies
build.ps1             Existing Windows Framework compiler build
test.ps1              Isolated x86 test runner
package.ps1           Installation and source ZIP creation
```

## Native patch map

| Native method | Hook | Purpose |
| --- | --- | --- |
| `ItemSlot.GetBulkBuyAmount` | Postfix | Replace the base buying batch while respecting finite stock. |
| `ItemSlot.HandleShopSlot` | Transpiler | Cap the computed loop count after vanilla repeat acceleration. |
| `Main.HoverOverCraftingItemButton` | Transpiler | Replace the Shift crafting multiplier. |
| `CraftingRequests.CraftItem` | Prefix | Bound recipe executions by complete output stacks. |
| `ItemSlot.GetBulkCraftAmount` | Postfix | Reflect the configured capacity-bounded crafting amount. |

Transactions still use the original game's price, payment, stock, material, and delivery paths. Disabled/non-Shift paths keep vanilla behavior.

## Configuration validation

Core's integer conversion can round fractions; its conversion of doubles to strings can also lose fractional digits. The quantity properties therefore store normalized integer strings, with a scoped Harmony prefix at `ConfigPropertyMeta.CoerceValue` rejecting raw floating values before conversion.

The guard checks the property's exact declaring type and the two quantity field names. It leaves other mods' settings unchanged. The larger conversion method is used because Core's small `SetValue` wrapper may already be JIT-inlined.

The guard is installed before initial disk loading and also on mod initialization, including reuse of an existing configuration after unload. It is removed on unload or patch initialization failure.

## Limits and lifecycle

Buying is bounded by the requested count, cursor capacity, and finite stock. Crafting divides remaining cursor capacity by the recipe's per-execution output, rounding down. Arithmetic handles corrupt/oversized inputs without integer overflow; the effective capacity never exceeds the item's own limit or 9999.

The mod checks the executable version and both expected native IL patterns before installing native patches. Failed initialization disables callbacks and removes owned patches. Branch labels are preserved when inserting the shop capacity guard.

The release targets Terraria 1.4.5.8. The installed Core assembly reports version 0.4.2.0 while its internal framework identifier remains 0.4.1. Its manifest parser also rejects four-component Terraria version constraints, so the executable check is performed at runtime instead.

## Verification boundaries

`test.ps1` executes the assertion runner as x86 to match Terraria's architecture. It resolves the game's embedded managed dependencies and redirects `Program.SavePath` into `build/game-sandbox` before game-type initialization.

Tests inspect and patch the real local game methods, exercise capacity callbacks with real game objects, and verify Core config persistence. They do not launch the game entry point or open user saves. Actual UI interaction and multiplayer require separate manual verification.

The maintainer reported successful in-game verification on 2026-11-15. Multiplayer and arbitrary third-party patch combinations remain unverified.
