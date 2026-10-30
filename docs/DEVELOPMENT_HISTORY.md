# Development history

This chronology consolidates versioned design notes, local Git checkpoints and validation records. Public history starts with the sanitised snapshot; private machine records and inspection artifacts are not published. Earlier versions were called Food Crafting UI. Offline checks, maintainer reports and measured runtime results are distinct evidence.

## 25 October 2026 — research and 0.1 foundation

The original goal was native food-recipe navigation at Cauldron and Food Preparation Table without changing progression or crafting. Installed assemblies, prefab identities, recipe reconstruction, assets and input were investigated before implementation. CraftIndex and CraftSort informed API research only; no source/assets were incorporated. BepInEx/Harmony sufficed without Jotunn.

Guarded lifecycle integration, pure classifiers/queries, native search, layout restoration and scoped input followed. Stable station/asset/output identities and recoverable plugin-only favourites avoided translated-name keys and save changes. Runtime loading/rendering were not inferred from compilation.

## 26 October — 0.2–0.6 controls, discovery and provenance

Direct Type/Sort groups replaced cycling controls. Ready/craftable-only and the old selected-recipe favourite Pin were removed; row stars and favourites-only stayed. The later tracker pin is independent. Evaluating representative foods changed the proposed 60% threshold to overlapping positive stats at 50% of the maximum, including balanced/Eitr foods and verified edible conversions.

Biome squares used player-specific discovery, never recipe availability as a discovery proxy. Unknown names/colours/future positions were hidden. Font-independent stars, equal-size squares, compact search, fitted tooltip backgrounds and Misc naming refined presentation.

The biome overhaul reviewed all 69 then-installed food recipes. Explicit provenance, complete/partial ingredient evidence, alternative ingredients, common supplies and curated themes remained distinct; mixed sources kept multiple associations. Reliable coverage rose from 42 to 67, with two intentionally uncertain cases. Factual fixtures and discovery-mask/ambiguity tests supported this, without claiming visual validation.

## 26–27 October — 0.7–0.8 equipment

Workbench/Forge Craft added eight overlapping equipment categories from native item/skill/tool/projectile metadata, independent Game/A–Z sorting and collapsed discovered-biome controls. Upgrade/Repair stayed native. Black Forge followed verified station/recipe inspection, shared native category icons and expanded ingredient mappings. Estimated-biome tildes were removed from recipe names without changing inference.

## 27 October — 0.9–0.13 tracker, Galdr and Mead

The separate HUD tracker gained multiple stable pins scoped to character/world, Materials/Recipes tabs, direct ingredient aggregation, carried-only counts, quality/alternative planning, minimisation, scrolling and remembered positions. Missing/unknown pins stayed safe. Inventory notifications drove updates; favourites remained separate.

Galdr Table reused equipment infrastructure. Mead Ketill initially shared food presentation, then gained effect-based Healing/Stamina/Eitr/Resistance/Other categories through fermentation metadata, with no food badges or biome controls. Food stations adopted collapsed biome disclosure without recipe refreshes.

The tracker became compact and dark-backed with `owned/required` counts. Redundant reorder arrows were removed. A Mead category click failing until A–Z was clicked was traced to an omitted query-equality field and fixed; classification itself was unchanged. Early F8 hide/edit behaviour was later retired.

## 28 October — 1.0.21 compatibility and 0.14–0.15 UI

After the actual game update, 192 relevant API records and 126 crafting UI records were compared: no relevant signature/access/type break; a native scrolling touch guard was noted. Baked Poteitr, Kale Chips and Oven Pancake moved from Cauldron to Food Preparation Table. Narrow live-structure-gated identity aliases preserved development favourites/pins; ingredients, yields and biome logic stayed unchanged. Old factual fixtures remain purposeful compatibility baselines.

Subtle rounded controls/tooltips, 65% tooltip backing and H/S/E colour markup reused native assets. The tracker frame inherited the game's environment-lit material. Crafting controls then followed native closing animation before cleanup.

Tracker interaction moved to normal inventory access, with Settings dragging and a separate display pin for passive gameplay visibility. F8 and two redundant patches were removed, leaving nine normal targets. These changes retained saved positions and recipe identities.

## 29 October — 0.16–0.17 inventory, storage and pointer repair

Stationless Craft gained adaptive Filters, All/Equipment/Food/Mead/Other families, contextual categories, shared search/favourites/pins and Food/Equipment biomes. Actual personal/world no-cost grants were distinguished from debugmode. Only vanilla availability enters queries; canonical identities prevent cross-view duplicates and creative-only pins become anonymous after access ends. A 481-recipe audit found 43 stationless assets without identity collisions.

Storage added explicit category Sort & Stack for 11 inspected chest/cart/ship roots. Metadata-compatible merging, local ownership/access checks, exact packet validation and a synchronous original-reference journal precede one native save notification. Custom-data stacks never merge; unsafe or unsettled states decline. No custom networking/server dependency. Pure fault injection, quantity conservation and idempotence tests supplement still-required non-host multiplayer tests.

Visible but unclickable tracker controls were repaired by placing the interactive panel in the native inventory canvas and returning it to HUD for passive display. The maintainer confirmed recovery; no failed live raycast proved a particular filter button caused the regression.

## 29 October — 0.18 public release

Project/assembly/namespace/plugin paths became CraftyQoL (`io.github.zinnkio.craftyqol`); preferences intentionally started fresh without deleting old files or saves. A separate publication snapshot preserved MIT terms and used noreply metadata. Private history, proprietary assemblies/assets and internal audits were excluded.

Discovery explicitly gated biome tooltip name lookup; recipe tooltips stopped reporting biome provenance. Tracker visibility investigation showed it was present but overlapped inventory, so only its new default moved to top centre. Existing positions stayed intact.

Public 0.18.2 shipped with installation downloads, early AI disclosure and acknowledgements. Release validation passed 691 regression/API/compiled checks and two tooltip checks. The later improvements below are included in 0.18.3.

## 30 October — creative-opening measurement and local refinements

Opening a large creative list caused a reported 300–500 ms hitch. Opt-in instrumentation measured main-thread phases and native/owned row counts. Its first capture was invalid for full-mod comparison: the diagnostic Harmony owner triggered the compatibility guard. Exact registered-hook recognition, startup self-checks and observed-integration validity fields repaired the diagnostic. The initial capture was excluded.

Three sequential sessions each had one first opening plus nine repeats, with 399 displayed rows and matching recorded access/recipe/pin counts. Prepared templates and bounded reuse of owned row widgets reduced repeated median synchronous Show from 163.678 to 61.851 ms and owned row setup from 115.716 to 14.578 ms. Repeated capture-window peak frame intervals fell from 300.171 to 201.289 ms; a features-disabled control measured 121.054 ms. The control retained BepInEx/diagnostic hooks and was not pure vanilla. Managed allocations were unavailable. Native row construction and filtering did not improve; phase totals overlap and cannot be added.

The reuse implementation passed 712 full checks plus two tooltip checks and targeted profiler contracts. The maintainer found inventory Craft functionality acceptable with actual no-placement-cost, but still reported opening/filter hitches. Broader station/controller/scaling acceptance remains open.

Native recipe-row backgrounds were subsequently restored to square styling while retaining rounded controls/tooltips; eight focused checks passed. The maintainer reported improved appearance, not a new timing result. Craft pin height then followed Craft, and container Sort followed Stack All; 29 focused checks passed, visual alignment remains manual. No further speedup is claimed. The reported Sort-unavailable-until-item-move case remains deferred with its safety check intact.

## 30 October — repository/package cleanup

Historical reports and one-off inspection tools were archived privately. Current architecture/build/config/compatibility/manual guidance and this chronology replace duplicate records. All functional source, tests and seven factual fixtures were retained. Packaging now keeps build provenance beside the archive; the player ZIP contains only the plugin, installation notes, MIT licence and third-party notices. Cleanup changed no gameplay, schema or Harmony behaviour and did not itself create a release.

## Validation approach

Pure classifications, every discovery mask, query composition, atomic preference recovery, tracker math and fault-injected storage plans are tested independently of Unity. Installed API/body and compiled-plugin contracts catch integration drift. Small edits use targeted checks; larger integration/packaging milestones justify a full suite. Compilation does not prove rendering, multiplayer delivery or all mod combinations; see [manual checks](MANUAL_TESTS.md).

## 30 October — 0.18.3 release checkpoint

The already installed local implementation was promoted to a distinct patch version after cleanup: bounded owned-row reuse, square native recipe rows, native-height Craft pin/Sort controls and optional opening diagnostics. Only version metadata and its identity assertion changed for release preparation. Settings, favourites, pins, schemas and normal Harmony scope remain unchanged. Release packaging keeps the four-file player ZIP separate from developer provenance.

Earlier creative inventory feedback and measured reuse improvements remain limited to their documented scope. Large-list hitches and the conservative first-open Sort packet mismatch remain known; visual sizing, broader controller/scaling and non-host multiplayer acceptance are not implied by release publication.
