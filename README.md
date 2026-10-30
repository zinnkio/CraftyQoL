# CraftyQoL

Craft faster. Find recipes, track materials and organise storage without leaving Valheim's native UI.

**AI disclosure:** CraftyQoL was created using OpenAI Codex, with maintainer-led in-game testing and feedback.

**[Download the latest release](https://github.com/zinnkio/CraftyQoL/releases/latest)** · [Installation](docs/INSTALLATION.md) · [Report a bug](https://github.com/zinnkio/CraftyQoL/issues/new)

**Valheim 1.0.21** · **BepInExPack 5.4.2351** · **Client-side**

See the [changelog](CHANGELOG.md) for released and upcoming changes. [In-game screenshots](https://github.com/zinnkio/CraftyQoL/releases/tag/v0.18.3#screenshots) showcase the features in v0.18.3; v0.18.4 adds native button styling.

## Features

- **Find recipes:** search localised names, sort recipes and save favourites.
- **Food filters:** overlapping Health, Stamina and Eitr categories with independent stat sorting.
- **Mead and equipment filters:** potion-effect categories and native equipment-category icons.
- **Spoiler-aware biomes:** filter by discovered biomes only. Future biome names stay hidden; recipe tooltips omit biome classifications.
- **Recipe tracker:** pin multiple recipes, combine their carried-material requirements and check progress as `owned/required`. Drag the panel in Settings or pin it on-screen during gameplay. New trackers start at top centre.
- **Inventory crafting:** compact filters with contextual categories, including large no-cost crafting lists.
- **Sort & Stack:** explicitly organise the opened container by category and consolidate compatible stacks. Player inventory and hotbar stay untouched.

Works with **Cauldron, Food Preparation Table, Mead Ketill, Workbench, Forge, Black Forge, Galdr Table** and the stationless inventory Craft panel. Crafting requirements and progression remain vanilla; Upgrade and Repair are unchanged.

Storage support includes standard chests, barrel, grausten chest, wardrobe, parked carts, Karve, Longship and Drakkar. [Compatibility details](docs/COMPATIBILITY.md).

## Install

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and close the game.
2. Download **CraftyQoL-0.18.4.zip** from the [release page](https://github.com/zinnkio/CraftyQoL/releases/tag/v0.18.4).
3. Open the ZIP and copy its **BepInEx** folder into your Valheim installation, merging with the existing folder. The DLL belongs at `BepInEx/plugins/CraftyQoL/CraftyQoL.dll`.
4. Launch Valheim. No server installation is needed.

A standalone DLL is also available on the release page. Updating CraftyQoL preserves its settings, favourites and pins. If replacing the old Food Crafting UI development build, remove its DLL from the active plugins folder so both versions do not load together; keep it elsewhere for rollback.

## Controls

| Action | Control |
| --- | --- |
| Search | Search field or Ctrl+F; X clears |
| Favourite | Star beside a recipe; toolbar star filters favourites |
| Track a recipe | Pushpin beside Craft or Ctrl+P |
| Use the tracker | Open inventory with its normal binding, usually Tab |
| Move or keep tracker visible | Tracker Settings: drag the header or select the display pin |
| Organise storage | Sort button in the opened container header |

Saved tracker positions are retained. [Configuration and preferences](docs/CONFIGURATION.md) · [Full installation guide](docs/INSTALLATION.md).

## Compatibility

Windows PC / Valheim 1.0.21 is the supported baseline. Only clients using the mod need BepInEx/CraftyQoL; storage uses vanilla synchronisation.

Other crafting organisers or inventory mods may conflict. Sorting declines unsupported or unsafe inventories, and stacks with custom item data remain separate. Creative mode, controller/UI-scale combinations and non-host multiplayer have not been exhaustively tested. [Known limitations](docs/COMPATIBILITY.md).

## Thanks

Thanks to **[CraftIndex](https://github.com/jg224/CraftIndex)** by **jg224** and **[CraftSort](https://github.com/GrandpaUA/CraftSort)** by **GrandpaUA**. Auditing these projects helped identify Valheim APIs and compatibility considerations. CraftyQoL is independently implemented; their source code and assets are not included.

Thanks also to the [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX) maintainers.

[Build from source](docs/BUILDING.md) · [Development history](docs/DEVELOPMENT_HISTORY.md) · [MIT licence](LICENSE) · [Third-party notices](THIRD_PARTY_NOTICES.md)

Not affiliated with Iron Gate or Coffee Stain. Game assets and dependencies are not bundled.
