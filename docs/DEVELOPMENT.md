# Development notes

## Structure

```text
src/
  AssemblyInfo.cs      Assembly version and net48 target metadata
  AutoStowState.cs     Limit notification and18-tick transfer state machine
  AutoStow.cs          Fresh native activity observers and inventory placement
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
| `ItemSlot.HandleShopSlot` | Prefix/postfix + transpiler | Observe held shop activity, bound the loop, and handle a capacity limit. |
| `Main.HoverOverCraftingItemButton` | Prefix/postfix + transpiler | Observe focused recipe activity, configure the multiplier, and handle a capacity limit. |
| `CraftingRequests.CraftItem` | Prefix | Bound recipe executions by complete output stacks. |
| `ItemSlot.GetBulkCraftAmount` | Postfix | Reflect the configured capacity-bounded crafting amount. |

Transactions still use the original game's price, payment, stock, material, and delivery paths. Disabled/non-Shift paths keep vanilla behavior.

## Configuration validation

Core's integer conversion can round fractions; its conversion of doubles to strings can also lose fractional digits. The quantity properties therefore store normalized integer strings, with a scoped Harmony prefix at `ConfigPropertyMeta.CoerceValue` rejecting raw floating values before conversion.

The guard checks the property's exact declaring type and the two quantity field names. It leaves other mods' settings unchanged. The larger conversion method is used because Core's small `SetValue` wrapper may already be JIT-inlined.

The guard is installed before initial disk loading and also on mod initialization, including reuse of an existing configuration after unload. It is removed on unload or patch initialization failure.

Config schema2 raises the minimum quantity to10, keeps the maximum9999 and defaults10, and adds AutoStow=false plus LimitSound=true (subordinate to AutoStow).

## Auto-stow

Fresh native shop/focused-recipe observations identify the active source, held mouse button and cursor stack. A capacity-limit event emits one optional built-in MenuTick sound immediately, then waits18 simulation ticks. A draw-end observer cancels missing activity without assuming that drawing runs at60fps. Release, pause, focus loss, UI/config/world changes also cancel pending work.

For crafting, a remainder too small for a whole recipe is effectively full. Observers honor UsingOrReusingItem, IsLockedFromCrafting and the native release-after-selection flag before and after hover processing; pending server craft grants defer all stowing. Native GetItem places the held object into personal inventory with sound/text/void-vault routing/coin merging disabled and returns the remainder to the cursor. No-progress transfers block until the interaction restarts. A snapshot plus compatible-inventory count reconstructs the unplaced remainder if a pickup callback throws after mutation, avoiding a cursor alias or duplicated already-placed stack. Successful nonstacking crafted-item transfers rearm the native recipe-repeat marker; ordinary stacking recipes retain the recipe-change guard.

## Limits and lifecycle

Buying is bounded by the requested count, cursor capacity, and finite stock. Crafting divides remaining cursor capacity by the recipe's per-execution output, rounding down. Arithmetic handles corrupt/oversized inputs without integer overflow; the effective capacity never exceeds the item's own limit or 9999.

The mod checks the executable version and both expected native IL patterns before installing native patches. Failed initialization disables callbacks and removes owned patches. Branch labels are preserved when inserting the shop capacity guard.

The release targets Terraria 1.4.5.8. The installed Core assembly reports version 0.4.2.0 while its internal framework identifier remains 0.4.1. Its manifest parser also rejects four-component Terraria version constraints, so the executable check is performed at runtime instead.

## Verification boundaries

`test.ps1` executes the assertion runner as x86 to match Terraria's architecture. It resolves the game's embedded managed dependencies and redirects `Program.SavePath` into `build/game-sandbox` before game-type initialization.

Tests inspect and patch the real local game methods, exercise capacity callbacks with real game objects, and verify Core config persistence. They do not launch the game entry point or open user saves. Actual UI interaction and multiplayer require separate manual verification.

The maintainer reported successful1.0.0 in-game verification on2026-11-15. Auto-stow in1.1.0 has automated coverage; actual UI behavior, multiplayer and arbitrary third-party patch combinations remain unverified.
