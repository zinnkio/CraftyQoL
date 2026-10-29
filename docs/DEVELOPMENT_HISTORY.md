# Development history

This chronological summary is based on pre-publication Git checkpoints, versioned design records and validation notes. Public Git history begins with a sanitised source snapshot; private history, machine settings and internal audit files are not published. Earlier versions were named **Food Crafting UI**. Implementation, static tests and user feedback are distinguished from full in-game validation.

## 25 October 2026 — concept, investigation and 0.1

The initial goal was a lightweight client-side extension to food crafting: preserve the native window, avoid floating crafting overlays, and improve navigation without changing progression, recipes, requirements or consumption. Cauldron and Food Preparation Table were investigated first; other benches were deliberately outside initial scope.

Research covered BepInEx/Harmony, actual game assembly signatures, native recipe-list construction, station prefab identities, item statistics, lifecycle, UI assets and input. Existing mods were technical API references only. Independent source/specification and minimal dependencies were explicit requirements. BepInEx/Harmony were sufficient, so no Jotunn dependency was introduced. Source inspection and compilation were never treated as game loading/rendering evidence.

The first implementation established guarded initialization, native toolbar/recipe-list integration, scoped keyboard/controller input and layout cleanup. Pure category/query/sorting code was separated from Unity presentation. Stable IDs used exact station, recipe asset and output prefab rather than translated names. Recoverable plugin-owned favourites, row nutrition symbols, audited packaging and manual checks followed. No game binaries or assets were redistributed.

## 26 October 2026 — 0.2 direct controls and discovery

Visual refinement removed the selected-recipe toolbar Pin/favourite shortcut and craftable-only Ready feature, including unused eligibility/polling paths. Row favourites/favourites-only stayed. This obsolete Pin was distinct from the later independent recipe tracker pin.

Cycling controls became directly selectable All/H/S/E and Game/H/S/E groups with independent numerical sorting and overlapping category membership. The research proposal of60% of the highest food stat was evaluated, not assumed; the implemented rule uses50% with positive-stat handling and intermediate-food relationships. Search dimensions, selection feedback, native styling, keyboard/controller routes and lifecycle restoration were refined.

Compact biome squares were gated by player-specific discovery records, not available recipes or inferred recipe origins. Undiscovered names, identifying colours and future positions were suppressed; uncertainty failed closed. Pure geometry/disclosure/input tests supported implementation, with visual/scaling acceptance explicitly manual.

## 26 October 2026 — 0.5 presentation and 0.6 provenance

The0.5 pass refined shared toolbar geometry, native control surfaces, selection and star styling. Subsequent changes matched biome/sort square dimensions, renamed uncertain classification to Misc, fitted backgrounds to wrapped tooltip text and made inline favourite buttons square. Font-independent outline/filled stars gained controller access. Tooltips initially received50% opaque backing; later0.14 increased it for readability.

The0.6 overhaul reviewed all69 then-installed food-station recipes and ingredient provenance. Explicit output associations, ingredient-derived evidence and uncertainty remained distinct. Mixed ingredients produced multiple biome memberships; partial evidence preserved known associations; alternative ingredients retained only shared reliable origins. Common/domestic/trader supplies did not silently imply progression. Curated feast/serpent themes and exact-key user overrides remained documented exceptions. Intermediate cooked products followed verified conversion relationships.

Reviewed factual fixtures, ambiguous/modded/missing inputs and every discovery-mask combination were tested. Known associations improved from42 to67 of69, with two intentionally uncertain cases;200 combined offline checks passed at that milestone. These results supported classifier evidence, not live UI rendering. Managed cached-query timing/allocation was recorded separately from Unity frame cost.

## 26–27 October 2026 — 0.7 equipment and 0.8 Black Forge

Workbench/Forge equipment Craft support used verified native item types, skills, damage/projectile and recipe metadata. Eight categories allowed overlaps without misusing food statistics. Search, favourites, Game/A-Z sorting and collapsed discovered-biome controls reused the existing view/query architecture. Upgrade/Repair isolation was required.

The0.8 pass replaced labels with native category icons and added Black Forge after station/recipe/lifecycle inspection. Equipment/ingredient classification expanded without duplicating the UI/query pipeline or adding Harmony patches. Estimated-biome tildes disappeared from recipe names, while provenance/tooltips/inference remained. Food classification was preserved.

## 27 October 2026 — 0.9 persistent tracker

The tracker introduced an independent HUD lifecycle separate from crafting toolbar state and favourites. Multiple unique stable recipe pins were scoped to character/world through plugin-owned JSON, without editing saves. Direct ingredients aggregate for one craft per recipe, respecting quantities/output amounts and quality/alternative planning. Only carried inventory counts; no chest search or recursive subcrafting occurs.

Materials/Recipes tabs, expanded notes/removal, minimisation, scrolling, remembered position and safe missing/unknown restoration followed. Native inventory notifications invalidate counts; cached metadata avoids scene/storage scanning. Pure aggregation/persistence tests and native API contracts preceded manual HUD/controller acceptance. Vanilla consumption/unlocks remained untouched.

## 27 October 2026 — 0.10 Galdr, 0.11 Mead and 0.12 refinement

Galdr Table reused equipment Craft/tracker infrastructure after targeted qualification. Mead Ketill then gained filtering/pinning; the detail pin became a square pushpin beside Craft. Tracker visibility across chest access was addressed.

Mead initially reused food-oriented presentation.0.12 replaced this with dedicated All/Healing/Stamina/Eitr/Resistance/Other effect categories. Fermentation links connect bases to final effects; restoration/regen/resistance can overlap, utility/unknown goes Other. Food-stat badges and Mead biome controls were removed. Food stations adopted equipment's default-collapsed biome disclosure, without rebuilding recipes for disclosure changes.

The tracker became compact, dark-backed and content-sized, with internal scrolling, native icons and owned/required quantities. Individual expanded requirements reused that notation. Multiple pins, material math and persistence remained. Redundant reorder arrows were subsequently removed while preserving insertion order. Early edit/hide interaction used F8; later versions replaced it.

## 27 October 2026 — 0.13 refresh/visibility fixes

A Mead category filter appeared to update only after A-Z was selected. Unchanged-query comparison omitted the potion category; adding every effective predicate restored independent refresh. This was query equality, not altered effect classification.

Tracker visibility expanded across inventory/stations with hide/restore refinement. The versions preserved native availability/crafting, favourites, material aggregation and safe persistence. User feedback drove UI changes; automated checks and in-game reports stayed separate evidence levels.

## 28 October 2026 — 1.0.21 compatibility / 0.13.1

The game update completed before the audit proceeded from1.0.20 to1.0.21. Relevant crafting, recipe, inventory and HUD APIs/private bindings were compared instead of regenerating unrelated recipe audits.192 relevant API records and126 crafting UI records were inspected: no relevant signature/access/type changes, with a native scrolling touch guard noted.

Baked Poteitr, Kale Chips and Oven Pancake moved from Cauldron to Food Preparation Table. A narrow actual-structure-gated compatibility path preserved existing development-plugin favourites/pins where required; biome inference/gameplay were unchanged.45 targeted persistence/compatibility cases plus API/compiled checks supported the update. This historical alias is distinct from the later deliberately fresh rebrand preferences.

## 28 October 2026 — 0.14 styling, 0.14.1 lighting and 0.15 lifecycle

Texture-preserving rounded mesh effects refined owned buttons, selection borders, rows and fitted tooltips. Existing Health/Stamina/Eitr colours applied to corresponding tooltip words/values. Tooltip opacity increased to65%. Native textures/materials/fonts/feedback remained; original state restored on scope exit. No custom texture or global shader replacement was added.

The tracker frame inherited native environment-lit UI material/tint while preserving dark backing/readable text. No guessed time-of-day colour polling was introduced. Rendered lighting/readability still required manual observation.

Crafting controls then remained attached/noninteractive during native close animation instead of disappearing before the window moved away. Cleanup followed the verified hidden state; rapid reopen and Upgrade/unsupported cleanup stayed explicit.

Tracker interaction switched to normal inventory access. Settings exposed dragging and a separate persistent display pin; default unpinned visibility followed inventory. F8/eye controls and two redundant input patches were removed, leaving nine targeted Harmony methods. Optional v1 keepVisible preserved tracker documents without changing recipe identities.648 full offline checks plus two integration checks passed at this milestone.

## 29 October 2026 — 0.16 inventory Craft

Stationless inventory Craft gained an adaptive Filters toggle for large creative lists: All/Equipment/Food/Mead/Other families, appropriate contextual categories, shared search/favourites/Game-A-Z/pinning, and Food/Equipment-only biomes. Manual disclosure lasts for the inventory session; compact geometry restores when hidden/closed.

Player.NoCostCheat, world NoCraftCost and AllRecipesUnlocked were investigated separately; debugmode alone was not a no-cost grant. Filtering receives only vanilla availability. Access changes refresh that source; creative-only tracker pins become anonymous safely when grants/knowledge disappear.

Canonical identities remain independent of the viewed interface, avoiding station/inventory duplicates. Inventory origin is separate from identity. A targeted481-asset audit found43 stationless assets and no canonical collisions. Pure5,000-record, session/geometry/knowledge cases supported the extension;662 full offline checks plus two integration checks passed. Normal inventory filtering later received user confirmation; creative transitions remain unverified in game.

## 29 October 2026 — 0.17 storage and 0.17.1 pointer repair

Storage research verified11 chest/cart/ship roots, legitimate open/ownership transfer, inventory serialization and native change/save/dirty replication. Explicit native-header Category sorting conservatively consolidates compatible partial stacks and packs top-left. Exact runtime metadata/shared references, capacities, quantities and proposed packets are validated; every nonempty custom-data stack stays separate.

A synchronous original-reference journal avoids intermediate move callbacks. One qualified native change notification publishes validated state after source/saved-byte/authority rechecks. Unpublished uncontaminated local changes may restore; confirmed publication is never overwritten by old network state. Unknown schemas/callbacks/critical patches/unsettled synchronization decline. No server mod, custom RPC, ownership bypass, player inventory sort or cross-container transfer.

Twenty-four storage cases include120 generated idempotence inventories and transaction fault injection, plus native/compiled authority contracts.0.17 passed688 full offline checks and two tooltip checks. The user later confirmed ordinary sorting; exhaustive preservation/persistence and non-host player-hosted/dedicated multiplayer tests remain open.

The tracker was reported visible with Settings/tabs but unclickable after inventory UI additions.0.17.1 hosted its existing interactive panel under native inventory Canvas, returning to HUD for passive display, preserving HUD coordinates/scale/drag and saved position.24 targeted checks passed and the user confirmed interaction worked again. No failed live raycast was captured; this supported diagnosis does not prove any particular filter/Sort button caused the regression.

## 29 October 2026 — 0.18 CraftyQoL public preparation

Project/assembly/namespace/workspace/build/test references and plugin-owned paths were renamed. CraftyQoL0.18.0 uses GUID io.github.zinnkio.craftyqol and CraftyQoL.dll. Preferences intentionally start fresh; old files/settings/saves remain untouched. All53 runtime files were compared with the working baseline under name/metadata substitutions to prevent functional changes.

A private backup preceded renaming. A separate allowlisted publication snapshot starts fresh public history with noreply commit metadata; private history/paths/settings, inspection/decompilation files, proprietary libraries/assets, archives and internal reports are excluded. Existing MIT terms and historical copyright notice are retained; no new licence was selected.

Release builds without warnings/errors;688 full regression/API/compiled-plugin plus two tooltip checks pass, including identity and nine-patch scope. No renamed game launch/deployment is claimed. Optimisation review is local/report-only and authorises no runtime modifications.

## Testing/performance evolution

Coverage grew from pure queries/classification/geometry to reviewed factual fixtures, exhaustive discovery masks, atomic preference recovery, tracker aggregation/knowledge/privacy and fault-injected storage transactions. Installed assembly/body and compiled-plugin contracts constrain integration without redistributing game code. Small changes use targeted checks; broader milestones use justified full regression.

Inspection, compilation, pure tests, user reports, rendered UI validation and multiplayer profiling remain distinct. Cached metadata, debounced search, pooled rows and inventory notifications avoid obvious repeated work; disclosure alone does not rebuild recipes. Synthetic managed timings are not Unity frame-time/network evidence. Creative/controller/scaling/exhaustive storage/network acceptance remains in [MANUAL_TESTS.md](MANUAL_TESTS.md).
