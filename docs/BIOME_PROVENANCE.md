# Biome provenance

Current classifier evidence lives in `Core/BiomeCatalogue.cs`, food/equipment metadata and the [reviewed fixtures](../tests/CraftyQoL.Tests/Data/README.md). The historical recipe audit is consolidated in [development history](DEVELOPMENT_HISTORY.md); machine-specific inspection reports are privately archived.

Explicit output associations take precedence. Ingredient-derived associations retain mixed biomes and distinguish complete from partial evidence. Alternatives use reliable shared evidence; common/domestic/trader ingredients do not silently imply progression. Exact curated feast/serpent themes and user overrides remain explicit exceptions. Intermediate food follows verified conversion relationships, not name heuristics. Unknown associations display Misc.

Association is never player discovery. The palette separately gates names, colours and eligibility using actual player knowledge; recipe tooltips do not expose provenance. See [architecture](ARCHITECTURE.md) and [override configuration](CONFIGURATION.md). Older factual fixtures remain required until targeted new game evidence justifies updating them.
