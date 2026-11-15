# Shift Batch

**English** | [简体中文](README.zh-CN.md)

A client-side [TerrariaModder](https://github.com/Inidar1/terraria-modder) mod for configuring how many items you buy or craft while holding Left Shift.

## Features

- Separate Shift batch settings for buying and crafting.
- Optional automatic inventory transfer when the held stack reaches capacity.

## Requirements

- Windows 10 or 11.
- Terraria 1.4.5.8.
- TerrariaModder Core 0.4.2.

The mod supports vanilla NPC shops and the vanilla crafting interface. Custom shop/crafting interfaces may use their own rules.

## Installation

1. Download the installation ZIP from the [latest release](https://github.com/gene9831/terraria-shift-batch/releases/latest).
2. Install it as a local archive in Vault, or extract its `TerrariaModder` folder into your Terraria installation.
3. Start the modded game and open **Shift Batch's configuration page** in the mod manager.

To uninstall, close the game and remove `TerrariaModder/mods/shift-batch`. To disable the mod temporarily, turn off **Enabled** in its configuration.

## Usage

Set the desired buying and crafting amounts on the configuration page. Hold **Left Shift** while buying or crafting as usual. Holding the mouse button continues to repeat the operation.

The crafting amount counts **recipe executions**. If a recipe produces 25 items, a setting of 100 requests up to 2500 items. Actual quantities are limited by cursor stack capacity, stock, currency and ingredients. A 25-output recipe with a stack limit of 9999 can produce 9975 before the cursor is effectively full.

### Automatic inventory transfer

Enable **Auto-stow** to continue across full stacks. Keep holding Shift and the same mouse button over the same shop item or recipe for about **0.3 seconds** after reaching capacity. The held items then move into personal inventory. Releasing the input or moving away cancels the delay.

If only part fits, the remainder stays on the cursor. If nothing fits, the item stays held until you restart the interaction. Items are never dropped to make room.

## Configuration

Changes apply immediately and save automatically.

| Setting | Default | Description |
| --- | --- | --- |
| Enabled | On | Enable configurable Shift batches. |
| Shift buy | 10 | Items per buying batch; integer from 10 to 9999. |
| Shift craft | 10 | Recipe executions per crafting batch; integer from 10 to 9999. |
| Auto-stow | Off | Move full cursor stacks into personal inventory after the delay. |
| Debug logging | Off | Write diagnostic information to the framework log. |

Out-of-range values are clamped to 10–9999. Invalid input keeps the previous valid value. Capacity limits also respect each item's own stack limit, including recipes that produce multiple items.

## Building from source

Install Terraria, TerrariaModder Core, the game's XNA runtime and the Windows .NET Framework C# compiler. Open PowerShell in the repository root and supply your game installation path:

```powershell
$terraria = 'C:/Games/Terraria'
./build.ps1 -TerrariaPath $terraria
./package.ps1 -TerrariaPath $terraria
```

The DLL is written to `build/ShiftBatch.dll`; installation and source ZIPs are written to `dist/`.

To run the tests with the same installation path:

```powershell
./test.ps1 -TerrariaPath $terraria
```

## Troubleshooting

If quantities differ from the requested amount, check cursor capacity, currency, stock, ingredients and crafting conditions. For loading errors, check that the game and Core match the requirements above.

For diagnostics, enable **Debug logging** on the configuration page and reproduce the issue. Look for `[shift-batch]` in the latest `TerrariaModder/core/logs/terrariamodder.client.session-*.log` file. Turn debug logging off when finished.

## Contributing and license

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and bug-report guidance. Released under the [MIT License](LICENSE.txt).

Built with [TerrariaModder](https://github.com/Inidar1/terraria-modder) and [Harmony](https://github.com/pardeike/Harmony).
