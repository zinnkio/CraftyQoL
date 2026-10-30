# Installation

Requires Valheim **1.0.21** and [BepInExPack_Valheim **5.4.2351**](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). Client-side; no server installation required.

## Install or update

1. Close Valheim and install BepInEx if needed.
2. Download **CraftyQoL-0.18.3.zip** from [GitHub Releases](https://github.com/zinnkio/CraftyQoL/releases/tag/v0.18.3).
3. Copy the ZIP's **BepInEx** folder into your Valheim folder, merging with the existing folder. Copy only that folder; the remaining ZIP files are documentation/provenance.
4. Confirm `BepInEx/plugins/CraftyQoL/CraftyQoL.dll` exists, then launch normally.

For a manual DLL installation, put the release's standalone `CraftyQoL.dll` at the same path. Keep one copy of the plugin. Settings, favourites and recipe pins are retained when updating CraftyQoL.

Replacing the old Food Crafting UI development plugin: move its old DLL outside the active plugins folder before installing. The rebranded identity has separate preferences; old settings are not deleted or automatically migrated.

## Quick start

Open a supported Craft tab or inventory Craft to use filters, search and favourites. Click a recipe's pushpin or Ctrl+P to track it. Open inventory (usually Tab) to use the tracker; Settings enables dragging and the separate display pin. New trackers start top-centre; saved positions stay where you put them.

Open storage and press Sort for category sorting/stack consolidation. Custom-data stacks remain separate. Upgrade/Repair and crafting rules stay vanilla.

## Troubleshooting and removal

Check `BepInEx/LogOutput.log` for CraftyQoL 0.18.3. Confirm the game/loader version, a single installed copy and no conflicting recipe/inventory organiser. Unsupported storage or uncertain native access/ownership/synchronisation declines sorting.

To uninstall or roll back, close the game and remove/replace only CraftyQoL's DLL. Preserve plugin configuration if desired; no character/world save edits are needed. [Report an issue](https://github.com/zinnkio/CraftyQoL/issues/new) with versions, steps and a relevant log excerpt, removing personal details first.

[Configuration](CONFIGURATION.md) · [Compatibility](COMPATIBILITY.md) · [Build from source](BUILDING.md)
