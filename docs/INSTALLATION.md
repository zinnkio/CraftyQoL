# Installation and controls

Target: Valheim **1.0.21**, Unity **6000.0.75f1**, BepInExPack_Valheim **5.4.2351**. Windows PC is inspected. Client plugin; servers/other players need not install it.

1. Install BepInEx separately. Build `CraftyQoL.dll` from [source](BUILDING.md), or use an audited release DLL if provided.
2. Close Valheim normally. Put only `CraftyQoL.dll` under `BepInEx/plugins/CraftyQoL/`.
3. Replacing Food Crafting UI development builds: move the old DLL out of the active plugin tree and keep for rollback. Never load both identities. Do not delete other mods/configurations or saves.
4. Launch and inspect `BepInEx/LogOutput.log` for CraftyQoL0.18.0. New identity starts fresh; no old-plugin migration/deletion.

Controls appear in supported Craft tabs and stationless inventory Craft. Upgrade/Repair/unrelated recipes remain native. Click category/sort buttons. Ctrl+F opens/focuses Search; X clears. Row stars toggle favourites; search-row star filters. Pushpin beside Craft or Ctrl+P toggles a recipe pin.

Open inventory (normal binding, usually Tab) to use tracker Materials/Recipes and recipe controls. Settings exposes dragging, hide-completed and display pin. Selected gold display pin keeps the panel passive during gameplay; Done leaves Settings. F8 mode is retired.

Storage **Sort** affects only the current qualified container. Custom-data stacks remain separate. Uncertain access/ownership/synchronisation/incompatible mods disable or decline sorting; see tooltip/log.

Controller: native toolbar/recipe routes, tracker shoulder entry from final inventory group, storage Up from top row; Submit activates, B/shoulders return. Normal inventory close remains available. Controller/scaling acceptance is manual.

Uninstall with game closed by removing only CraftyQoL's DLL. Retain preferences if desired. Roll back a preserved known DLL with game closed; identities have separate preferences. Never edit world/character saves to install/reset/remove.

[Configuration](CONFIGURATION.md) · [Compatibility](COMPATIBILITY.md) · [Manual acceptance](MANUAL_TESTS.md).
