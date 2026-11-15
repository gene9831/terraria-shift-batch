# Development notes

## Structure

```text
src/
  AssemblyInfo.cs      Assembly version and net48 target metadata
  AutoStowState.cs     Capacity detection and 18-tick transfer state machine
  AutoStow.cs          Fresh native activity observers and inventory placement
  BatchRules.cs        Integer normalization and stack-capacity arithmetic
  BatchConfig.cs       Separate mod settings and validation warnings
  ConfigInputGuard.cs  Scoped protection before Core numeric coercion
  DebugLog.cs          Optional diagnostics and scoped logger threshold
  NativePatches.cs     Native quantity hooks and IL transformations
  Mod.cs               Initialization, patch lifecycle, and warning display
tests/Tests.cs         Executable assertions against local dependencies
tests/Test-Package.ps1 Real packaging/stale-file/version checks
build.ps1             Existing Windows Framework compiler build
test.ps1              Isolated x86 test runner
package.ps1           Installation and source ZIP creation
```

## Native patch map

| Native method | Hook | Purpose |
| --- | --- | --- |
| `ItemSlot.GetBulkBuyAmount` | Postfix | Replace the base buying batch while respecting finite stock. |
| `ItemSlot.HandleShopSlot` | Transpiler | Bound the native purchase loop by cursor capacity. |
| `ItemSlot.LeftClick(Item[], int, int)` | Prefix/postfix | Observe shop input even when native full-cursor routing returns early. |
| `ItemSlot.RightClick(Item[], int, int)` | Prefix/postfix | Observe right-button shop input without resetting left-button activity. |
| `Main.HoverOverCraftingItemButton` | Prefix/postfix + transpiler | Observe focused recipe activity, configure the multiplier, and handle a capacity limit. |
| `CraftingRequests.CraftItem` | Prefix | Bound recipe executions by complete output stacks. |
| `ItemSlot.GetBulkCraftAmount` | Postfix | Reflect the configured capacity-bounded crafting amount. |

Transactions still use the original game's price, payment, stock, material, and delivery paths. Disabled/non-Shift paths keep vanilla behavior.

## Configuration validation

Core's integer conversion can round fractions; its conversion of doubles to strings can also lose fractional digits. The quantity properties therefore store normalized integer strings, with a scoped Harmony prefix at `ConfigPropertyMeta.CoerceValue` rejecting raw floating values before conversion.

The guard checks the property's exact declaring type and the two quantity field names. It leaves other mods' settings unchanged. The larger conversion method is used because Core's small `SetValue` wrapper may already be JIT-inlined.

The guard is installed before initial disk loading and also on mod initialization, including reuse of an existing configuration after unload. It is removed on unload or patch initialization failure.

Config schema 4 keeps the minimum quantity 10, maximum 9999, defaults 10 and AutoStow=false. It adds DebugLogging=false, preserves existing quantity/auto-stow settings, and omits the obsolete sound property. The schema counter is only for configuration migration; it is independent of the product version.

## Diagnostics

DebugLogging is a client-side checkbox on Shift Batch's configuration page. It defaults off and updates live through OnConfigChanged. DebugLog captures this mod logger's original MinLevel; enabling lowers only that logger to Debug, while disabling/unloading restores the original value. Its Write method also gates every optional message, so a global Debug setting does not override a disabled switch. Core/global/other-mod logger settings are not changed. Warn/Error retain their normal paths.

Configuration snapshots, runtime versions/module build identity, patch readiness and world lifecycle are low-frequency messages. Auto-stow reports the start of a delay, cancellation, transfer quantities and blocked results only on state transitions. IsPending and the Started decision expose those transitions without changing the delay or capacity calculations. A blocked full inventory emits no per-frame retry logs. Logs go through Core's shared/session logging and recent-log facilities; the mod creates no separate log file.

Logger tests retain real Core formatting, filtering and recent-log collection while suppressing file/console output in the isolated test process.

## Version and packaging

Product versions are defined in manifest.json and AssemblyInfo.cs. Runtime debug lines include the module identifier to distinguish builds.

package.ps1 derives ZIP names from manifest.version and recreates its checked project-local build/package staging directory. The runtime ZIP contains exactly ShiftBatch.dll, manifest.json and LICENSE.txt. README documents are source-only. Tests seed stale README/binary files before rebuilding and verify the exact ZIP file list and version consistency. Updating a merged installation must separately remove the two obsolete README files; ZIP extraction alone cannot remove them.

## Auto-stow

Fresh native shop/focused-recipe observations identify the active source, held mouse button and cursor stack. Effective capacity starts an 18-simulation-tick delay. A draw-end observer cancels missing activity without assuming that drawing runs at 60 fps. Release, pause, focus loss, UI/config/world changes also cancel pending work.

Shop observation wraps the large LeftClick/RightClick methods, before PickItemMovementAction rejects a full cursor. HandleShopSlot alone is insufficient: a full cursor bypasses it on the left path, and the inactive right-button call can reset a pending left-button observation. Unrelated slot contexts and inactive buttons are ignored; missing shop activity still cancels at draw end. The small Handle dispatcher may be JIT-inlined, so it is not patched. Pending native inventory actions prevent shop stowing before and after processing.

For crafting, a remainder too small for a whole recipe is effectively full. Observers honor UsingOrReusingItem, IsLockedFromCrafting and the native release-after-selection flag before and after hover processing; pending server craft grants defer all stowing. Native GetItem places the held object into personal inventory with sound/text/void-vault routing/coin merging disabled and returns the remainder to the cursor. No-progress transfers block until the interaction restarts. A snapshot plus compatible-inventory count reconstructs the unplaced remainder if a pickup callback throws after mutation, avoiding a cursor alias or duplicated already-placed stack. Successful nonstacking crafted-item transfers rearm the native recipe-repeat marker; ordinary stacking recipes retain the recipe-change guard.

## Limits and lifecycle

Buying is bounded by the requested count, cursor capacity, and finite stock. Crafting divides remaining cursor capacity by the recipe's per-execution output, rounding down. Arithmetic handles corrupt/oversized inputs without integer overflow; the effective capacity never exceeds the item's own limit or 9999.

The mod checks the executable version and both expected native IL patterns before installing native patches. Failed initialization disables callbacks and removes owned patches. Branch labels are preserved when inserting the shop capacity guard.

This build targets Terraria 1.4.5.8. The installed Core assembly reports version 0.4.2.0 while its internal framework identifier remains 0.4.1. Its manifest parser also rejects four-component Terraria version constraints, so the executable check is performed at runtime instead.

## Verification boundaries

`test.ps1` executes the assertion runner as x86 to match Terraria's architecture. It resolves the game's embedded managed dependencies and redirects `Program.SavePath` into `build/game-sandbox` before game-type initialization.

Tests inspect and patch the real local game methods, exercise capacity callbacks with real game objects, and verify Core config persistence. Purchase regressions call the real ItemSlot.Handle dispatcher with production patches installed, retaining native LeftClick/RightClick and transaction routing; hover presentation alone is skipped in the headless fixture. Tests also simulate a blocked native inventory-action guard. They do not launch the game entry point or open user saves. Actual UI interaction and multiplayer require separate manual verification.

For public-dependency GitHub-hosted builds and tag publication, see [CI.md](CI.md). The temporary server-derived CI reference never enters a release archive.
