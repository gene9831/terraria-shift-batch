# Shift Batch

**English** | [简体中文](README.zh-CN.md)

Configure how many items you buy or how many times you execute a crafting recipe while holding **Left Shift** in Terraria. Shift Batch is a small client-side mod for [TerrariaModder](https://github.com/Inidar1/terraria-modder).

Buying and crafting have separate settings. Both default to **10**, preserving the vanilla batch size, and accept values from **1 to 9999**. Actual operations are bounded by the item's remaining stack capacity.

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

- Separate Shift buying and crafting settings in the **F6** mod menu.
- Defaults of **10 items** per buying batch and **10 recipe executions** per crafting batch.
- Live configuration updates with automatic persistence.
- Integer validation, range clamping, and retention of the previous value after invalid input.
- Capacity checks for an empty cursor, existing compatible stacks, and recipes producing multiple items.
- Vanilla prices, payment, stock, ingredient checks, and hold-to-repeat behavior remain in use.
- An enable/disable setting restores vanilla behavior without removing the mod.

## Requirements

| Component | Tested version / requirement |
| --- | --- |
| Operating system | Windows; the upstream framework targets Windows 10 and 11 |
| Terraria | **1.4.5.8**; this release checks the executable version |
| TerrariaModder Core | Built and tested against assembly **0.4.2.0** |
| Launcher | TerrariaModder Vault or `TerrariaInjector.exe` |

Install the framework using its [installation guide](https://inidar1.github.io/terraria-modder/installation/). Build dependencies are listed under [Building from source](#building-from-source).

## Installation

1. Obtain `ShiftBatch-1.0.0.zip`, or build it from source with `package.ps1`.
2. In Vault, install the ZIP as a local mod archive. Alternatively, merge the ZIP's `TerrariaModder` folder into the folder containing `Terraria.exe`.
3. Restart the modded game through Vault or `TerrariaInjector.exe`.
4. Open **F6 → Shift Batch**.

Expected layout:

```text
<Terraria folder>/
└── TerrariaModder/mods/shift-batch/
    ├── ShiftBatch.dll
    ├── manifest.json
    ├── README.md
    ├── README.zh-CN.md
    └── LICENSE.txt
```

To uninstall, close the game and remove this mod's `shift-batch` folder. To temporarily restore vanilla behavior, turn off **Enabled / 启用** in the mod's settings.

## Usage

1. Press **F6**, select **Shift Batch**, and edit either batch setting.
2. Click its value, enter an integer, then press Enter or click outside the field. The change saves and applies immediately.
3. In a vanilla NPC shop, hold **Left Shift** and buy an item as usual.
4. In the vanilla crafting interface, hold **Left Shift** and craft as usual.

**Crafting counts recipe executions, not individual output items.** If a recipe produces 25 items each time, a setting of 100 requests up to 2500 items before capacity and material limits are applied.

Holding the mouse button continues to repeat and uses vanilla acceleration. A batch setting is not a total target for the entire hold.

## Configuration

| F6 setting | JSON key | Default | Allowed values |
| --- | --- | --- | --- |
| Enabled / 启用 | `Enabled` | `true` | `true`, `false` |
| Shift buy / 购买数量 | `PurchaseBatch` | `"10"` | Integer text, 1–9999 |
| Shift craft / 制作次数 | `CraftBatch` | `"10"` | Integer text, 1–9999 |

Configuration is saved to:

```text
<Terraria folder>/TerrariaModder/core/configs/shift-batch.client.json
```

```json
{
  "_version": 1,
  "Enabled": true,
  "PurchaseBatch": "10",
  "CraftBatch": "10"
}
```

- Values above 9999 become 9999; values below 1 become 1.
- Letters, fractions, blank input, and integer overflow keep the previous valid value, initially 10. A warning is logged and shown in game when a world is active.
- Use ordinary integer strings when editing JSON. Raw floating-point/scientific values are rejected before Core can round or stringify them.

### Stack-capacity examples

The effective capacity is the item's `maxStack`, with a defensive ceiling of 9999, minus compatible items already held by the cursor. Incompatible cursor items stop the operation. Buying also respects limited stock and available currency; crafting retains vanilla ingredient and crafting-condition checks.

| Scenario | Maximum additional operation |
| --- | --- |
| Buying, stack limit 9999, cursor already holds 9900 | Buy 99 items |
| Crafting 25 items per recipe, empty cursor, stack limit 9999 | Execute 399 times → 9975 items |
| Crafting 25 items per recipe, cursor already holds 9900 | Execute 3 times → 75 additional items |

Crafting stops when there is insufficient space for a complete recipe output. Output is not partially discarded or automatically sent to another container. Capacity checks also apply after vanilla hold acceleration.

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

The current automated suite passes **119 assertions**, covering input validation, Core configuration loading/saving, raw-number precision edge cases, stack arithmetic, actual game IL transformations, native Harmony patch installation/removal, lifecycle handling, and capacity checks with real game objects in an isolated x86 process.

Tests do not start the game entry point or open user characters/worlds. Their save path points to `build/game-sandbox`.

**Manual verification:** the maintainer reported successful in-game verification on **2026-11-15**. Multiplayer behavior has not been verified.

## Compatibility and limitations

- Targets vanilla NPC shops and the vanilla mouse crafting interface. Storage Hub and other custom crafting interfaces keep their own rules.
- This is a client-side TerrariaModder mod. Multiplayer transactions and arbitrary combinations of third-party patches are not verified.
- The tested Core DLL is assembly version 0.4.2.0, but its internal framework identifier reports 0.4.1. The manifest uses a minimum identifier of 0.4.1 to avoid a false warning; this does not establish support for every Core build.
- Native methods are checked before patch installation. An unsupported Terraria version or instruction layout disables this mod's patches and logs an error.

See [development notes](https://github.com/gene9831/terraria-shift-batch/blob/main/docs/DEVELOPMENT.md) for implementation details.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Shift Batch is missing from F6 | Check the folder layout and `manifest.json`, then restart using the modded launcher. |
| Shift still uses the default batch | Confirm the mod is enabled and the field edit was confirmed. Check the latest Core log for patch errors. |
| Fewer items are bought/crafted than requested | Check cursor capacity, incompatible items, currency, stock, materials, and crafting conditions. |
| Build reports a missing reference | Verify `-TerrariaPath`, Core/Harmony installation, and the XNA runtime assemblies. |
| Compiler or reference assemblies are missing | Install the required .NET Framework development tools, or use the SDK project with net48 reference assemblies. |

Logs: `<Terraria folder>/TerrariaModder/core/logs/terrariamodder.client.session-*.log`. Use the newest session and look for `[shift-batch]` entries.

## Contributing

See [CONTRIBUTING.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CONTRIBUTING.md) for the build/test workflow and bug-report guidance. Version history is recorded in [CHANGELOG.md](https://github.com/gene9831/terraria-shift-batch/blob/main/CHANGELOG.md).

## License and acknowledgments

Released under the [MIT License](LICENSE.txt). Terraria and third-party dependencies retain their own licenses and are not included in this repository.

Thanks to [TerrariaModder](https://github.com/Inidar1/terraria-modder), [Harmony](https://github.com/pardeike/Harmony), and the Terraria developers.
