# Architecture

CraftyQoL is an original client-side BepInEx/Harmony plugin. Runtime source is in `src/CraftyQoL`; `Core` contains pure classifications, queries, geometry, identities, persistence and transaction planning. Tests link that source and use test-only Mono.Cecil to inspect installed APIs and compiled contracts. Native assets are referenced at runtime, never distributed. No Jotunn dependency or transpilers.

## Crafting lifecycle

`Plugin`, `StationScope` and `GameContracts` qualify supported native Craft views and private bindings. `FoodView` owns the toolbar, row decorations and reversible layout measurements. Only the vanilla-provided available recipe list enters filtering; no injection, unlock or consumption changes. Prefix filtering reduces rows before native construction; the postfix restores requested ordering and decorates rows. Selection and scroll handling remain native-aware. Upgrade, Repair and unsupported stations are isolated.

`NativeToolbar` reuses native fonts, sprites, materials and input templates. Search is debounced with native focus/IME handling. Category and sorting are independent. Inventory Craft has contextual families and automatic disclosure for actual personal/world no-cost flags, not debugmode alone; manual disclosure wins for that inventory session. Biome expansion changes layout without rebuilding recipe data. Closing retains noninteractive children during the native animation, then restores original geometry; rapid reopen and immediate unsupported/Upgrade cleanup are handled separately.

Nine normal Harmony targets: `InventoryGui.UpdateCraftingPanel`, `UpdateRecipeList`, `Update`, `Hide`, `OnDestroy`; `ZInput.GetButtonDown`, `GetKeyDown`; `UIGamePad.ButtonPressed`; `PlayerController.TakeInput`. Scoped guards/finalizers restrict interception to owned controls. Foreign list ownership or incompatible private bindings disable the affected integration conservatively.

## Classification, discovery and identity

`RecipeMetadata` caches recipe facts; food conversion graphs, equipment metadata and fermented Mead effects drive separate classifiers. Positive food stats qualify at 50% of the highest relevant stat; overlaps and balanced food are intentional. Food intermediates require a unique verified edible result. Equipment categories can overlap. Mead restoration/regen/resistance are effect-based and have no biome palette or food badges.

Reviewed biome mappings and ingredient evidence retain multi-biome and partial/unknown provenance internally. Player discovery is a separate prerequisite: undiscovered names, colours and future slots stay hidden, uncertainty fails closed, and discovered choices without available matches are disabled. Recipe tooltips omit biome provenance. Search uses displayed localised names; deterministic sorting follows filtering.

Canonical keys use station, recipe asset and output prefab, not translated names or the currently viewed panel. Inventory and station views therefore share favourites/pins. Only unambiguous stationless identities persist. The narrow 1.0.21 food-move alias is gated on actual recipe structure. Favourites, tracker pins and tracker display pin remain distinct.

## Row controls and presentation

`RowWidgetPool` prepares a minimal native-style favourite template and retains at most 512 inactive owned widget sets per live GUI. Food stat text is lazy. The existing list prefix releases/detaches owned widgets before vanilla destroys rows; native rows are not pooled. Rebinding clears old tooltips, navigation and actions. `RowClickBinding`/`RowFavouriteButton` reject a mouse press spanning reuse; native keyboard/controller Submit remains inherited. Suspend returns widgets; final cleanup destroys the pool.

Native recipe-row backgrounds/highlights remain square. Rounded filter/favourite controls, fitted tooltips and the native environment-lit tracker frame retain game assets/tints. The recipe pushpin copies Craft's vertical anchors/pivot/offsets on layout activation; container Sort uses Stack All's measured height/Y. Their existing widths and native-control restoration remain unchanged.

The next source update copies only the native action-button visual properties (normal/hover/pressed/disabled sprites, image/material, transition and colour block). It does not clone native action callbacks. Stack All provides the common skin/font; the adjacent Craft button provides the pin skin. Compact buttons scale the sliced border by native-to-owned height; larger labels use bounded native font sizing. Selection keeps the original group marker and H/S/E colours; biome swatches tint the image without replacing native interaction sprites. Row favourites use the same skin in their existing cached template. No new component, frame polling, recipe refresh or Harmony patch is introduced. Search and native recipe-row backgrounds retain their current styling. Rendered layout remains a manual check.

## Tracker and persistence

`RecipeTracker`/`TrackerCatalogue` resolve multiple character/world-scoped pins and aggregate direct ingredients for one craft each, respecting yields, qualities and alternatives. Only carried inventory counts. Inventory notifications invalidate counts; bounded model checks and cached metadata avoid repeated scans. Missing/outdated/undiscovered pins stay stored anonymously and do not contribute totals. Creative-only pins are requalified when access ends.

`TrackerHud` owns a separate lifecycle with pooled rows, scrolling and content-sized geometry. Its interactive panel joins the native inventory canvas and returns to HUD for passive pinned display, retaining original HUD coordinates. Default visibility follows inventory; Settings offers dragging and a display pin. Menu/map/death/modal/drag guards protect input. No F8 mode or inventory-key replacement. Display changes do not regenerate recipe metadata.

Bounded versioned JSON in the plugin config directory uses atomic replacement and previous-good backup recovery. Writes coalesce; no character/world save access. See [configuration](CONFIGURATION.md) for schemas and override format.

## Storage integrity

`StorageBindings` checks native schemas, critical patch ownership and the sole native save callback. `StorageCatalogue` caches one primary item category. `StorageSnapshot` retains item/shared/custom references and exact metadata; `Core/StorageSorting` plans and validates stack/slot/quantity conservation. Custom-data stacks never merge.

Explicit click -> legitimate open/access/local ownership -> exact live/saved packet agreement -> validated plan/expected packet -> source/authority recheck -> synchronous original-reference journal -> applied validation -> one native `Inventory.Changed` -> native Container save/replication receipt. No player inventory sorting, transfers, custom RPC, ownership bypass or direct save-file writes. Only an unpublished, uncontaminated local journal may roll back; unknown outcomes disable sorting. A local receipt is not remote acknowledgement.

Known limitation: some newly opened containers decline with "Waiting for matching native container data" until a native item movement. The byte mismatch cause remains unmeasured; the safety check is intentionally retained.

## Performance and verification

Static facts are cached, search debounced, decorations/palette/tracker rows reused, and storage work is click-driven. Native recipe rebuilding and large-list hitches remain. Optional startup-only opening profiling adds six separately owned timing hooks; it is off by default, uses bounded records/deferred IO, and validates actual feature integration. Only its exact registered methods are exempt from the foreign-hook guard. Inclusive timings are not additive; unavailable allocation counters report null. No profiling source changes are part of repository cleanup.

[Build/check commands](BUILDING.md), [measured development results](DEVELOPMENT_HISTORY.md) and [manual acceptance](MANUAL_TESTS.md) distinguish static contracts, user reports and runtime evidence.
