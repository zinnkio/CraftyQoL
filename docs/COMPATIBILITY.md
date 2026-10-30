# Compatibility

Supported baseline: **Valheim 1.0.21**, Unity 6000.0.75f1, Windows PC and **BepInExPack_Valheim 5.4.2351** (BepInEx 5.4.23.5 / Harmony 2.9.0.0). Other game versions/platforms are not qualified.

## Crafting and tracking

Craft filters support Cauldron, Food Preparation Table, Mead Ketill, Workbench, Forge, Black Forge, Galdr Table and stationless inventory Craft. Upgrade, Repair and unrelated station lists retain vanilla behaviour. Only vanilla-provided recipes are filtered; no unlock/consumption changes.

Food/equipment biomes require actual player discovery. Future names/colours/slots are hidden; recipe tooltips omit biome classifications. Mead has effect-based categories and no biome controls. Translated recipe names/search are supported; control labels are mostly English.

Tracker counts carried inventory only, with direct ingredient requirements and quality/alternative planning. Missing or undiscovered pins remain anonymous. New trackers default top-centre; saved custom locations remain unchanged. Inventory access enables interaction; display pin keeps it passive during gameplay.

## Storage

Supported native roots: `piece_chest_wood`, `piece_chest`, `piece_chest_blackmetal`, `piece_chest_private`, `piece_chest_barrel`, `piece_chest_grausten`, `piece_chest_warderobe`, `Cart`, `Karve`, `VikingShip`, `VikingShip_Ashlands`. Wardrobe spelling is the game's actual identifier. Carts must not be pulled/in use.

Tombstones, auto-destroy loot and unknown/custom container types are excluded. Sorting never moves player inventory/hotbar items or transfers between containers. Compatible stacks require matching native properties/references and actual stack limits. **Nonempty custom-data stacks never merge**, even if the data matches.

Only legitimately opened, locally owned/in-use storage with valid access and matching saved state can sort. Unknown schemas, unsafe ranges, foreign critical patches/change handlers or unsettled synchronisation decline. Native replication is eventual; a local receipt is not a remote acknowledgement. No custom networking/server plugin/ownership bypass or direct save-file editing.

## Known limitations

- Large creative lists can still hitch on opening/filter changes; the unreleased source reduces owned-row setup but does not remove native UI costs.
- Some containers decline sorting with "Waiting for matching native container data" until a native item move. The mismatch cause remains unmeasured; no safety bypass is provided.
- Other recipe organisers and inventory/container mods may disable or conflict with integration; compatibility is conservative.
- Creative transitions, controller/UI-scale combinations and non-host multiplayer do not have exhaustive documented validation.
- Silent foreign metadata/inventory changes and newer game schemas are unqualified; fallback is Other/Misc or disabled integration.

The maintainer reports the mod deployable after in-game checks. Automated compilation/pure/static checks supplement that feedback; they do not prove every multiplayer or mod combination. [Manual test matrix](MANUAL_TESTS.md).
