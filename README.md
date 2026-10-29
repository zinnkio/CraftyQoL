# CraftyQoL

Native crafting navigation, persistent recipe tracking and storage organisation for **Valheim**.

CraftyQoL extends the game's existing interfaces with compact controls. It filters the recipes Valheim already provides; it never unlocks recipes or replaces crafting requirements. Storage sorting is an explicit operation on the opened container, using native inventory persistence and synchronisation.

**Version:** 0.18.0 · **Target inspected:** Valheim 1.0.21 / Unity 6000.0.75f1 · **Plugin:** `io.github.zinnkio.craftyqol`

This is the first public preview. The renamed Release build passes 690 offline checks. Earlier builds received user confirmation for station UI, normal inventory filters, storage sorting and the tracker click repair. Creative-mode transitions, exhaustive storage data safety, controller/scaling and non-host multiplayer acceptance remain open; compilation is not runtime verification.

## Features

- **Food:** overlapping Health, Stamina and Eitr categories; independent descending stat sorting or vanilla Game order; recipe stat indicators. A positive stat qualifies at 50% of the food's highest stat. Intermediate cooking products use verified finishing relationships.
- **Mead:** All, Healing, Stamina, Eitr, Resistance and Other, classified through effects and fermentation metadata. Game/A-Z sorting; no food badges or biome selector.
- **Equipment:** All, Weapons, Armour, Shields, Tools, Ammunition, Magic and Other, with overlapping categories and native icons. Game/A-Z sorting.
- **Search and favourites:** case-insensitive localised-name search, a compact clear action, row-star favourites and an independent favourites-only filter. Stable identities and plugin-owned preference files.
- **Biome filters:** collapsed food/equipment palettes expose confirmed discoveries only. Multi-biome, partial and uncertain associations retain their provenance; Misc is the fallback. Discovery is never inferred from available recipes.
- **Recipe tracker:** pin several recipes independently of favourites. Aggregate carried materials as `owned/required`, or expand individual recipe notes. Native item icons, scrolling, minimisation and a movable panel. Settings contains a separate display pin to keep the tracker visible during gameplay. Pins/preferences are scoped to character/world without editing their saves.
- **Inventory Craft:** an adaptive Filters toggle with All/Equipment/Food/Mead/Other families and contextual categories. Actual no-cost flags can auto-expand it; `debugmode` alone cannot. Manual expansion wins for the current inventory session. Uses only vanilla-provided recipes, including creative availability when actually granted.
- **Storage Sort & Stack:** one explicit Category sort, conservative partial-stack consolidation and consecutive top-left packing. Never sorts player inventory/hotbar or transfers items between containers.

## Supported crafting interfaces

| Interface | Mode |
| --- | --- |
| Inventory Craft, without a workstation | Recipe families and contextual filters |
| Cauldron | Food |
| Food Preparation Table | Food |
| Mead Ketill | Mead |
| Workbench | Equipment |
| Forge | Equipment |
| Black Forge | Equipment |
| Galdr Table | Equipment |

Station filtering is restricted to **Craft**. Upgrade, Repair and unrelated station recipe lists retain vanilla behaviour. The tracker can remain visible across inventory, chests and station tabs.

Storage support is restricted to seven verified wood/reinforced/black-metal/personal/barrel/grausten/wardrobe containers, parked Cart, Karve, Longship and Drakkar. Unsupported storage, tombstones and auto-destroy loot containers decline sorting. See [compatibility](docs/COMPATIBILITY.md).

## Installation and controls

1. Install the separately supplied [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). Qualified pack: **5.4.2351** (BepInEx 5.4.23.5, Harmony 2.9.0.0).
2. With Valheim closed, put your built `CraftyQoL.dll` in `BepInEx/plugins/CraftyQoL/`.
3. Remove the previous development plugin DLL from the active plugin search path if upgrading from Food Crafting UI; do not run both. Preserve it elsewhere for rollback. Existing configuration files and game saves do not need deletion.
4. Launch normally and check `BepInEx/LogOutput.log` for CraftyQoL 0.18.0.

No server installation, Jotunn or custom networking is required. Other players do not need CraftyQoL; storage uses native replication, but remote visibility/persistence still need multiplayer tests. Windows PC is the inspected platform; other platforms/versions are unqualified.

| Action | Control |
| --- | --- |
| Search | Click Search; Ctrl+F focuses/expands it; X clears |
| Favourite | Recipe-row star; search-row star filters favourites |
| Pin/unpin recipe | Pushpin beside Craft or Ctrl+P |
| Interact with tracker | Open inventory with its normal binding, usually Tab |
| Move tracker / keep visible | Tracker Settings; drag header / select display pin; Done exits settings |
| Sort opened storage | Sort in its native header |
| Controller | Explicit toolbar/row navigation; tracker shoulder entry from final inventory group; storage Up from top row; Submit, B/shoulders return |

The old F8 edit mode is retired. Controller mappings and UI scales require manual validation. Details: [installation](docs/INSTALLATION.md), [configuration](docs/CONFIGURATION.md), [manual checks](docs/MANUAL_TESTS.md).

## Persistence and safety

Configuration: `BepInEx/config/io.github.zinnkio.craftyqol.cfg`. Favourites, biome overrides and scoped tracker preferences live under `BepInEx/config/io.github.zinnkio.craftyqol/`. The rebrand starts fresh; no previous development settings are migrated or deleted.

Storage merging requires matching prefab/shared references and exact native properties, within actual stack maxima. **Nonempty custom-data stacks never merge**, even when dictionaries match. Unknown schemas/subclasses, foreign critical patches/callbacks, dragging, invalid access/ownership and unsettled saved contents decline sorting. Source/proposed layout are validated before one native change notification. No direct save-file access, ownership bypass or custom RPC. See [design](docs/DESIGN.md).

## Building and validation

Use .NET SDK **10.0.402** (pinned in `global.json`), a legitimate Valheim 1.0.21 installation and separately acquired BepInEx core references. Plugin target: .NET Framework 4.7.2; test host: .NET 10. No game/Unity/loader assemblies or extracted assets are included.

Copy `Environment.props.example` to ignored `Environment.props` and supply installation/reference paths. Then:

```powershell
.\tools\dotnet.ps1 build CraftyQoL.sln -c Release --nologo
.\tools\dotnet.ps1 run --project tests/CraftyQoL.Tests -c Release --no-build
```

Output: `src/CraftyQoL/bin/Release/net472/CraftyQoL.dll`. [Building from source](docs/BUILDING.md) covers native-API/compiled-plugin checks, references and packaging. No build/package script installs, launches or changes the game.

Rename verification: **688 regression/API/compiled-plugin + 2 tooltip checks**, zero build warnings/errors. Pure/static checks do not establish rendering, creative transitions, network safety or Unity frame performance. Follow [manual acceptance](docs/MANUAL_TESTS.md), including non-host player-hosted and dedicated servers with unmodded observers.

## Project

- [Development history](docs/DEVELOPMENT_HISTORY.md) — chronological pre-publication milestones and evidence limits.
- [Architecture](docs/DESIGN.md) · [Compatibility](docs/COMPATIBILITY.md) · [Changelog](CHANGELOG.md).
- [MIT licence](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md).

Independently implemented; not affiliated with Iron Gate or Coffee Stain. No proprietary game source/assets redistributed.
