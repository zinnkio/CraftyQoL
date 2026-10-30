# Inventory opening profiling

Optional diagnostics are disabled by default and require restart. Close Valheim, set `Diagnostics.ProfileInventoryOpenings = true` in the plugin config, then restart. Confirm filters and row stars are active before measuring.

With the same character/world, access flags, recipe list, search/filter state and tracker pins, perform ten openings (two seconds open, two closed), then exit normally. Captures appear under `BepInEx/config/io.github.zinnkio.craftyqol/diagnostics/inventory-openings-*.jsonl`. Disable profiling and restart afterwards; do not leave it on for normal play.

Compare only `comparisonValid = true` records with matching counts/access flags. Separate the first opening from repeats. Feature-disabled controls still contain loader/diagnostic overhead and are not pure vanilla. Timings are inclusive, frame intervals cover other game work, and unavailable allocation counters are null. Do not add nested phase totals.

The static startup suffix about no optimisation predates row reuse; identify the build using the exact DLL/source and capture counters. Opening timings neither validate interactions nor directly measure later filter-change hitches. Review logs for personal information before sharing. See [measured history and limitations](DEVELOPMENT_HISTORY.md), [configuration](CONFIGURATION.md) and [manual functional checks](MANUAL_TESTS.md).
