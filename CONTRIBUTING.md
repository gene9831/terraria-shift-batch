# Contributing

Bug reports, focused fixes, and documentation improvements are welcome. English and Simplified Chinese reports are both welcome.

## Development setup

1. Clone this repository.
2. Install Terraria 1.4.5.8 and TerrariaModder Core, including its Harmony dependency.
3. Make sure the game's XNA runtime prerequisites and the Windows .NET Framework C# compiler are available.
4. Run from the repository root:

```powershell
$terraria = 'C:\path\to\Terraria'
.\test.ps1 -TerrariaPath $terraria
.\build.ps1 -TerrariaPath $terraria
```

The scripts use the local game files as references. Do not commit or redistribute Terraria, Core, Harmony, or XNA binaries. Generated output and test sandbox data are ignored by Git.

## Changes

- Keep runtime code compatible with the C# 5 compiler used by `build.ps1`.
- Add meaningful regression coverage for input handling, quantity calculations, or patch/lifecycle changes.
- Preserve vanilla payment, stock, ingredient, condition, and delivery behavior.
- Run the full automated suite and test the affected behavior in game when possible.
- State which Terraria/Core versions and multiplayer scenarios you actually tested.
- Update both READMEs and the changelog when behavior changes.

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the patch map and configuration design.

## Reporting a bug

Open a repository issue with:

- Terraria, TerrariaModder Core, and Shift Batch versions.
- Singleplayer or multiplayer, relevant other mods, and the two batch settings.
- The shop item or crafting recipe, cursor contents, and reproducible steps.
- Expected and actual behavior.
- Relevant `[shift-batch]` lines from the latest Core session log.

Include a short relevant log excerpt rather than an entire save, configuration collection, or unrelated session history. Remove personal data before posting.

## Pull requests

Describe the concrete problem, resulting behavior, and verification performed. Keep changes focused. Contributions are made under the project's [MIT License](LICENSE.txt).
