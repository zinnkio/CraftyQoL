# Architecture

Original BepInEx/Harmony client plugin. Runtime: `src/CraftyQoL`. Tests link pure Core source and use Mono.Cecil for installed-API/compiled-plugin contracts without loading the game.

## Crafting

Plugin/StationScope/GameContracts qualify native Craft lifecycle/private bindings. FoodView owns the native toolbar/row decoration and layout restoration. NativeToolbar/category graphics/rounded mesh effects reuse game styling/assets without distributing assets or floating crafting overlays.

RecipeMetadata, food/Mead graphs and equipment classifiers cache static facts. Pure queries compose family/category/search/favourites/discovery-gated biomes, then independently sort deterministically. Only vanilla availability enters filtering; recipes/unlocks/consumption stay native. Query equality includes every effective predicate. Biome disclosure changes geometry/navigation without recipe recomputation. Close animation retains noninteractive controls until hidden; Upgrade/unsupported cleanup remains immediate.

Nine Harmony targets: InventoryGui.UpdateCraftingPanel, UpdateRecipeList, Update, Hide, OnDestroy; ZInput.GetButtonDown, GetKeyDown; UIGamePad.ButtonPressed; PlayerController.TakeInput. Scoped/finalizer guards limit interception to owned controls. No transpilers.

Biome palette label/tooltip lookup is explicitly gated by player discovery before resolving a name. All/Misc are neutral controls. Recipe-row tooltips show names/nutrition/equipment-or-potion categories only; biome associations/provenance remain internal filter metadata. No discovery-triggered row-tooltip reconstruction is needed.

## Tracker

RecipeTracker/TrackerCatalogue/pure math resolve scoped stable pins and aggregate carried direct materials only. Inventory notifications invalidate counts; knowledge/availability protect restoration/creative exit. Missing/unavailable pins remain anonymous in storage. Favourites/recipe pins have separate stores; display pin is a separate preference.

TrackerHud reuses a bounded row pool/scroll viewport. Interactive panel joins inventory Canvas, passive panel returns to HUD. Layout/scale/drag use original HUD coordinates. Raycast/visibility/modal guards remain authoritative; no custom cursor/F8 mode. Settings/display changes do not regenerate recipe metadata.

## Storage

StorageBindings qualifies schemas/private fields, sole native save callback and critical patch ownership. StorageCatalogue caches primary categories using existing food/equipment/effect/smelter/alloy metadata. StorageSnapshot retains original item/shared/custom references and exact metadata. Core/StorageSorting plans/validates placements and quantity conservation.

Explicit click -> access/local owner/source/saved bytes -> plan/expected native packet -> recheck -> synchronous count/grid/list journal -> applied validation/authority recheck -> one Inventory.Changed -> native Container.Save/ZDO dirty replication -> exact packet/native revision receipt. No per-item partial callbacks, ownership setter, custom RPC or save-file writes. Item dictionaries never change; custom-data stacks never merge.

Only an unpublished uncontaminated journal with unchanged saved bytes can be restored locally. Confirmed publication is never overwritten with old state; unknown outcomes disable sorting. No player mutation/transfers/automatic sorting. Runtime/network acceptance remains [manual](MANUAL_TESTS.md).

## Persistence/performance

Bounded plugin JSON uses atomic replacement/backup recovery. Keys use station/asset/output, not translation; inventory origin does not duplicate identities. Rebrand GUID paths start fresh.

Static facts/IDs cached; search debounced; palette/tracker rows reused. Storage work only on explicit click. Native recipe reconstruction remains a potential Unity cost; pure timings are not frame profiles. No optimisation report is included in the public repository.
