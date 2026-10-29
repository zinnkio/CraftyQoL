# Compatibility and limitations

## Qualified inputs

- Valheim **1.0.21**, Unity **6000.0.75f1**; relevant signatures/bodies/private bindings/recipe facts/native assets inspected locally.
- BepInExPack_Valheim **5.4.2351** (BepInEx5.4.23.5 / Harmony2.9.0.0).
- Plugin net472; no Jotunn/extra runtime framework. Game/Unity/loader libraries are referenced, never bundled.
- Windows PC inspected; other platforms/versions unqualified.

The1.0.21 audit checked crafting/inventory/HUD APIs and Baked Poteitr, Kale Chips and Oven Pancake moving from Cauldron to Food Preparation Table. Identity follows actual recipe metadata. A narrow structure-gated compatibility alias remains for older-format plugin records; the rebrand starts fresh and does no old-plugin migration.

## Scope

Craft prefabs: `piece_cauldron`, `piece_preptable`, `piece_MeadCauldron`, `piece_workbench`, `forge`, `blackforge`, `piece_magetable`, plus stationless inventory Craft. Upgrade/Repair/unrelated lists excluded. Vanilla supplies availability/requirements/consumption; only copied displayed recipes are filtered.

| Storage root prefab | Standard slots |
| --- | --- |
| piece_chest_wood | 5 x 2 |
| piece_chest | 6 x 4 |
| piece_chest_blackmetal | 8 x 4 |
| piece_chest_private | 3 x 2 |
| piece_chest_barrel | 6 x 2 |
| piece_chest_grausten | 8 x 5 |
| piece_chest_warderobe | 5 x 10 |
| Cart | 6 x 3 |
| Karve | 2 x 2 |
| VikingShip | 6 x 3 |
| VikingShip_Ashlands | 8 x 4 |

Wardrobe spelling is the native ID. Cart/ships use native root ownership overrides. Raft/decorative treasure, tombstones, auto-destroy loot and unknown/custom containers unsupported. Modified capacities require bounded standard inventories/all qualification checks; inventory-expansion compatibility is not promised.

## Multiplayer/data safety

Client-only; no server plugin/custom RPC. Require legitimate open, local owner/in-use, private/ward/distance access and matching current/saved bytes. One native change notification drives container save/dirty replication. No ownership bypass/player inventory sorting/transfers.

Strict byte matching can decline unsettled handoffs/legacy packets. Reopen/wait for stock synchronisation; never force ownership/load. Foreign critical patches, nonstandard save handlers, unknown schemas/subclasses, invalid ranges/capacity/metadata and player item aliases decline. Nonempty custom data never merges. Arbitrary unsignalled foreign modifications are unqualified. Native replication is eventual; local save receipt is not remote acknowledgement.

## Other UI mods/localisation

Known competing recipe organisers and foreign recipe-list prefixes/transpilers can disable crafting integration. Storage separately rejects critical inventory/authority/serialisation patches or non-native change handlers. Identifiers are compatibility guards, not dependencies; combinations remain untested.

Recipe names/search are localised; most controls are English. Native player discovery gates biomes and fails closed on uncertainty. Modded base items/recipes use metadata or Other/Misc fallback; arbitrary schemas unsupported. Long names, IME, controllers and scales need manual checks.

## Evidence limits

Rebrand Release: zero warnings/errors, **688 full offline regression/API/compiled-plugin +2 tooltip checks**. Earlier builds received limited user confirmation for station UI, sorting, normal inventory filters and tracker repair. Renamed DLL not launched. Creative, exhaustive item preservation/persistence and non-host player-hosted/dedicated multiplayer remain open. No broad compatibility/frame-time/network-safety claim. [Manual tests](MANUAL_TESTS.md).
