# Configuration and persistence

GUID: `io.github.zinnkio.craftyqol`. The rebrand starts fresh; previous configuration, favourites and tracker files are neither migrated nor deleted.

BepInEx file: `BepInEx/config/io.github.zinnkio.craftyqol.cfg`.

| Section / key | Default | Meaning |
| --- | --- | --- |
| General / Enabled | true | Craft navigation, tracker and storage Sort & Stack |
| Diagnostics / LogRefreshes | false | Recipe-list refresh counts/timings for manual investigation |

No automatic storage sorting, chest-counting, network option or F8 edit binding exists.

## Session controls

Station filters/sort/search retain per-station session state. Inventory Craft starts All/Game/unfiltered each new inventory session. Filters auto-expands for actual NoCostCheat/world NoCraftCost; debugmode alone is insufficient. Manual disclosure wins until inventory closes. Collapse retains active filters. Food/Equipment have optional collapsed discovery-gated biomes; Mead has none.

## Favourites

`BepInEx/config/io.github.zinnkio.craftyqol/favourites.json` stores v1 keys with exact recipe station, recipe asset and output prefab, using URI-escaped segments. Translated names are not identities. Missing keys remain stored; ambiguous identities decline persistence. Row stars/favourites-only use this store independently of pins.

## Tracker

`BepInEx/config/io.github.zinnkio.craftyqol/tracker/<characterHex>-<worldHex>.json` stores v1 pins, insertion order, alternative/quality choices, expanded notes, normalised position, minimised state, tab, hide-completed and `keepVisible`. The display pin defaults false and is independent of recipe pins/favourites. No reorder control. One direct craft per pin respects actual input quantities/output amounts; no recursive ingredient expansion or chest search.

Missing/changed/unknown recipes remain anonymous/removable and excluded from totals. Creative inventory pins resolve only under current native availability/grants; strict knowledge resumes when those end. Character/world IDs scope preferences; no saves are read or edited directly.

Writes coalesce and use bounded atomic replacement/previous-good `.bak` recovery. Unsupported schema/corrupt/denied files fail conservatively with warnings. Back up before manual recovery and close the game first. Identifiers never construct arbitrary paths.

## Optional biome overrides

Read at startup, never rewritten: `BepInEx/config/io.github.zinnkio.craftyqol/biome-overrides.json`.

```json
{"version":1,"recipes":[{"key":"COPY_AN_ACTUAL_KEY_FROM_YOUR_FAVOURITES_FILE","biomes":["Meadows","BlackForest"]}]}
```

Names: Meadows, BlackForest, Swamp, Mountains, Plains, Mistlands, Ashlands, DeepNorth, Ocean, Unknown. Unknown alone/empty has no reliable membership and displays Misc; All is a query choice, not an association. Overrides replace association only; never discovery/unlocks/station scope/requirements. Invalid documents are ignored as a whole with warning and preserved.

Storage layout persists through native inventory change/save/replication, not plugin preference files. All custom-data stacks remain separate.
