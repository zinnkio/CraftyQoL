# Manual acceptance

Offline checks are prerequisites, not proof of UI/gameplay/multiplayer safety. Use disposable test items/world for safety cases. Renamed0.18.0 still needs runtime validation.

## Crafting/tracker

- [ ] Seven stations plus inventory Craft: categories/overlaps, independent sorts, search/clear/IME, favourites/pins. Actual crafting quantities/requirements/outputs remain vanilla. Upgrade/Repair/unrelated interfaces retain ordering/selection/layout.
- [ ] Food/Equipment biome disclosure/selection/reset; mixed/partial/Misc provenance; unknown names/colours/future slots hidden. Mead effects/no food badges or biomes. Long names/empty results/native scrolling/selection valid.
- [ ] Inventory actual no-cost on/off while open, debugmode alone, world NoCraftCost and AllRecipesUnlocked separately. Native availability/manual session disclosure, five families/categories/large lists; no stale creative disclosure.
- [ ] Cross-view canonical favourite/pin identity, restart/scoped preferences, unknown/modded/changed restoration and creative-only pins anonymised on exit. Carried-only totals/quality/alternative planning/output information.
- [ ] Tracker tabs/Settings/Done/minimise/recipe controls in inventory/chest/stations/Upgrade. Drag/display-pin/hide-completed, closed unpinned hidden/pinned passive. Position/size across host/scales; dragging/split/modals/map/menu/loading/death input guards. Normal inventory binding/controller entry/Submit/back.
- [ ] Rapid open/close/switch/animation, no duplicates, tooltip fit/rounding/day-night readability, several resolutions/scales, no relocated vanilla panels.

## Storage

- [ ] All11 storage roots: native header/button/tooltips/navigation/drag/split/Take All/Stack All; parked Cart and ship storage. Long/localised names/scales/reopen.
- [ ] Mixed category order/grouping/top-left packing/trailing empties; full/sparse/empty/max-size and repeated/no-op sorts.
- [ ] Exact quantity/metadata before/after: partial/full/modded maximums; differing prefab/quality/variant/durability/world/crafter/flags/custom data. Equal nonempty custom dictionaries also separate. No loss/duplication/transformation. Base modded items conservative; subclasses/custom inventories decline.
- [ ] Drag/modal/private/ward/distance/destruction/pulled cart/ownership/sync/contested open failures decline. No player/hotbar/other container mutation or resource transfer.
- [ ] Reopen/rejoin/relog/server restart layout/quantity/metadata persistence; unknown states disable without old-data overwrite.

## Multiplayer -both required

- [ ] **Non-host player-hosted server**, unmodded host/observer: legitimate open, sort, observer reopen and exact comparison; contested open/handoff/denial/delay/disconnect/reconnect/persistence, including cart/ships.
- [ ] **Non-host dedicated server without mod**, unmodded observer: repeat preceding matrix, server restart and every storage class. Verify native authority/access.

Earlier user feedback confirms station UI, normal inventory filters, sorting and tracker interaction on development builds. Creative/exhaustive safety/controller/scaling/multiplayer remain open; rebrand only has offline evidence.
