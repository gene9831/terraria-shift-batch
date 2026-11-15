# Shift Batch

**English** | [简体中文](README.zh-CN.md)

Configure how many items you buy or how many times you execute a crafting recipe while holding **Left Shift** in Terraria. Shift Batch is a small client-side mod for [TerrariaModder](https://github.com/Inidar1/terraria-modder).

Buying and crafting have separate settings. Both default to **10**, preserving the vanilla batch size, and accept values from **10 to 9999**. Actual operations are bounded by the item's remaining stack capacity.

**Version 1.0 (metadata: 1.0.0).** Releases are built and published by GitHub Actions from approved version tags. See [CI and tag releases](docs/CI.md).

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Building from source](#building-from-source)
- [Testing](#testing)
- [Compatibility and limitations](#compatibility-and-limitations)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License and acknowledgments](#license-and-acknowledgments)

## Features

- Separate Shift buying and crafting settings on the **Shift Batch configuration page** in the mod manager.
- Defaults of **10 items** per buying batch and **10 recipe executions** per crafting batch.
- Live configuration updates with automatic persistence.
- Integer validation, range clamping, and retention of the previous value after invalid input.
- Capacity checks for an empty cursor, existing compatible stacks, and recipes producing multiple items.
- Vanilla prices, payment, stock, ingredient checks, and hold-to-repeat behavior remain in use.
- An enable/disable setting restores vanilla behavior without removing the mod.
- Optional delayed auto-stow lets you keep holding Shift and the mouse button across full stacks. It is **off by default**.
- Optional debug logging for configuration, runtime/patch details and auto-stow state changes, off by default and applied immediately.

## Requirements

| Component | Tested version / requirement |
| --- | --- |
| Operating system | Windows; the upstream framework targets Windows 10 and 11 |
| Terraria | **1.4.5.8**; this build checks the executable version |
| TerrariaModder Core | Built and tested against assembly **0.4.2.0** |
| Launcher | TerrariaModder Vault or `TerrariaInjector.exe` |

Install the framework using its [installation guide](https://inidar1.github.io/terraria-modder/installation/). Build dependencies are listed under [Building from source](#building-from-source).

## Installation

1. Download [ShiftBatch-1.0.0.zip](https://github.com/gene9831/terraria-shift-batch/releases/download/v1.0.0/ShiftBatch-1.0.0.zip), or build it from source with `package.ps1`.
2. In Vault, install the ZIP as a local mod archive. Alternatively, merge the ZIP's `TerrariaModder` folder into the folder containing `Terraria.exe`.
3. Restart the modded game through Vault or `TerrariaInjector.exe`.
4. Open the mod manager and select **Shift Batch → configuration**.

Expected layout:

```text
<Terraria folder>/
└── TerrariaModder/mods/shift-batch/
    ├── ShiftBatch.dll
    ├── manifest.json
    └── LICENSE.txt
```

The installation ZIP contains only these three files. Both READMEs remain in the source repository and source ZIP. When upgrading an older local build, remove its leftover `README.md` and `README.zh-CN.md` from this mod's folder; merging a ZIP does not delete existing files.

To uninstall, close the game and remove this mod's `shift-batch` folder. To temporarily restore vanilla behavior, turn off **Enabled / 启用** in the mod's settings.

## Usage

1. Open **Shift Batch's configuration page** in the mod manager and edit either batch setting.
2. Click its value, enter an integer, then press Enter or click outside the field. The change saves and applies immediately.
3. In a vanilla NPC shop, hold **Left Shift** and buy an item as usual.
4. In the vanilla crafting interface, hold **Left Shift** and craft as usual.

**Crafting counts recipe executions, not individual output items.** If a recipe produces 25 items each time, a setting of 100 requests up to 2500 items before capacity and material limits are applied.

Holding the mouse button continues to repeat and uses vanilla acceleration. A batch setting is not a total target for the entire hold.

### Optional auto-stow

Enable **Auto-stow / 满堆叠收包** on the mod's configuration page. When the cursor reaches its effective capacity, keep holding Shift and the same mouse button over the same shop item or focused recipe for about **0.3 seconds**; the held items then move into personal inventory and vanilla buying/crafting can continue. Both left-button and right-button purchases are supported.

Release Shift or the mouse button during the delay to cancel. Moving away from the target, changing the cursor item, pausing, losing window focus, or opening a blocking mod panel also cancels pending work. Native crafting restrictions, such as item reuse, inventory stacking, selecting a new recipe, or pending server craft grants, prevent auto-stow until the interaction becomes eligible again.

Native inventory placement can use compatible existing stacks, empty slots, and normal coin/ammo slots. If only part fits, the remainder stays on the cursor. If nothing fits, the item stays held and the mod waits for a restarted interaction instead of repeatedly retrying. It does not drop items or route them into a void vault or storage mod.

## Configuration

| Mod setting | JSON key | Default | Allowed values |
| --- | --- | --- | --- |
| Enabled / 启用 | `Enabled` | `true` | `true`, `false` |
| Shift buy / 购买数量 | `PurchaseBatch` | `"10"` | Integer text, 10–9999 |
| Shift craft / 制作次数 | `CraftBatch` | `"10"` | Integer text, 10–9999 |
| Auto-stow / 满堆叠收包 | `AutoStow` | `false` | `true`, `false` |
| Debug logging / 调试日志 | `DebugLogging` | `false` | `true`, `false` |

Configuration is saved to:

```text
<Terraria folder>/TerrariaModder/core/configs/shift-batch.client.json
```

```json
{
  "_version": 4,
  "Enabled": true,
  "PurchaseBatch": "10",
  "CraftBatch": "10",
  "AutoStow": false,
  "DebugLogging": false
}
```

- Values above 9999 become 9999; values below 10 become 10. Existing lower values migrate to 10. Capacity, stock, currency, or materials may still reduce an actual operation below 10.
- Letters, fractions, blank input, and integer overflow keep the previous valid value, initially 10. A warning is logged and shown in game when a world is active.
- Use ordinary integer strings when editing JSON. Raw floating-point/scientific values are rejected before Core can round or stringify them.

### Stack-capacity examples

The effective capacity is the item's `maxStack`, with a defensive ceiling of 9999, minus compatible items already held by the cursor. Incompatible cursor items stop the operation. Buying also respects limited stock and available currency; crafting retains vanilla ingredient and crafting-condition checks.

| Scenario | Maximum additional operation |
| --- | --- |
| Buying, stack limit 9999, cursor already holds 9900 | Buy 99 items |
| Crafting 25 items per recipe, empty cursor, stack limit 9999 | Execute 399 times → 9975 items |
| Crafting 25 items per recipe, cursor already holds 9900 | Execute 3 times → 75 additional items |

Crafting stops when there is insufficient space for a complete recipe output. With auto-stow enabled, this is also an effective limit: a 25-output recipe can stow its 9975-item stack rather than waiting for an unreachable 9999. Output is not partially discarded. Capacity checks also apply after vanilla hold acceleration.

## Building from source

### Prerequisites

- A local Terraria 1.4.5.8 installation with TerrariaModder Core and its Harmony dependency.
- The XNA assemblies installed with the game's runtime prerequisites.
- Windows PowerShell or PowerShell, and the .NET Framework C# compiler at `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`.

The primary scripts compile C# 5 against the installed dependencies. On the verified development machine, no additional SDK, Developer Pack, or Visual Studio download was needed.

### Build the DLL

Clone or download this repository, open PowerShell in its root, and set your own game path:

```powershell
$terraria = 'C:\Program Files (x86)\Steam\steamapps\common\Terraria'
.\build.ps1 -TerrariaPath $terraria
```

Output: `build/ShiftBatch.dll`. The scripts default to `D:\SteamLibrary\steamapps\common\Terraria` when `-TerrariaPath` is omitted; pass the parameter if your installation differs.

### Build installation and source archives

```powershell
.\package.ps1 -TerrariaPath $terraria
```

This runs the tests, builds the DLL, and writes installation/source ZIPs to `dist/`. Use `-OutputDirectory` to choose another output folder. Only the mod's own files are packaged; Terraria, Core, Harmony, and XNA binaries are referenced locally.

### Optional SDK project

`ShiftBatch.csproj` targets **.NET Framework 4.8 (`net48`)**. If using an installed .NET SDK, also supply the .NET Framework 4.8 reference assemblies through the Developer Pack or supported package restore:

```powershell
dotnet build .\ShiftBatch.csproj -c Release "-p:TerrariaPath=$terraria"
```

The SDK project is provided as an alternative; the verified build path is `build.ps1`.

## Testing

```powershell
.\test.ps1 -TerrariaPath $terraria
```

The current automated suite passes **272 assertions**, covering input validation, Core configuration loading/saving, raw-number precision edge cases, stack arithmetic, actual game IL transformations, native Harmony patch installation/removal, lifecycle handling, delayed transfer cancellation, native inventory placement/conservation and real Core debug-log filtering in an isolated x86 process.

Run `powershell -ExecutionPolicy Bypass -File .\tests\Test-Package.ps1 -TerrariaPath $terraria` to additionally verify the real packaging pipeline. This seeds stale documentation/binary files and confirms that the rebuilt installation ZIP contains exactly the three runtime files, source documentation is retained, and product/DLL versions match the manifest.

Tests do not start the game entry point or open user characters/worlds. Their save path points to `build/game-sandbox`.

**Manual verification:** the maintainer verified buying, crafting and the corrected auto-stow behavior in game on **2026-11-15**. The maintainer also verified the debug switch and its live logs. Final verification of the downloaded CI Release is performed separately. Multiplayer behavior has not been verified.

## Compatibility and limitations

- Targets vanilla NPC shops and the vanilla mouse crafting interface. Storage Hub and other custom crafting interfaces keep their own rules.
- This is a client-side TerrariaModder mod. Multiplayer transactions and arbitrary combinations of third-party patches are not verified.
- The tested Core DLL is assembly version 0.4.2.0, but its internal framework identifier reports 0.4.1. The manifest uses a minimum identifier of 0.4.1 to avoid a false warning; this does not establish support for every Core build.
- Native methods are checked before patch installation. An unsupported Terraria version or instruction layout disables this mod's patches and logs an error.

See [development notes](https://github.com/gene9831/terraria-shift-batch/blob/main/docs/DEVELOPMENT.md) for implementation details.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Shift Batch is missing from the mod manager | Check the folder layout and `manifest.json`, then restart using the modded launcher. |
| Shift still uses the default batch | Confirm the mod is enabled and the field edit was confirmed. Check the latest Core log for patch errors. |
| Fewer items are bought/crafted than requested | Check cursor capacity, incompatible items, currency, stock, materials, and crafting conditions. |
| Build reports a missing reference | Verify `-TerrariaPath`, Core/Harmony installation, and the XNA runtime assemblies. |
| Compiler or reference assemblies are missing | Install the required .NET Framework development tools, or use the SDK project with net48 reference assemblies. |

Logs: `<Terraria folder>/TerrariaModder/core/logs/terrariamodder.client.session-*.log`. Use the newest session and look for `[shift-batch]` entries.

For troubleshooting, enable **Debug logging / 调试日志** on **Shift Batch's configuration page**, reproduce the issue, then turn it off. No dedicated hotkey is added. Debug lines include runtime versions/build identity, current settings, patch readiness, and auto-stow waiting/cancellation/transfer/blocked results with item counts. Waiting and full-bag holds do not log every frame. The switch opens only this mod's logger threshold; no global logging setting needs changing. Warnings and errors keep using Core's normal logging path when debug is off.

## Contributing

See [CONTRIBUTING.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CONTRIBUTING.md) for the build/test workflow and bug-report guidance. Version history is recorded in [CHANGELOG.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CHANGELOG.md).

## License and acknowledgments

Released under the [MIT License](LICENSE.txt). Terraria and third-party dependencies retain their own licenses and are not included in this repository.

Thanks to [TerrariaModder](https://github.com/Inidar1/terraria-modder), [Harmony](https://github.com/pardeike/Harmony), and the Terraria developers.

CI also checks tag/version consistency, client API references and release SHA-256 checksums using verified public dependencies. See [docs/CI.md](docs/CI.md) for the cloud build and tag publishing process.
