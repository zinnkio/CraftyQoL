# Compatibility fixtures

These seven JSON files contain reviewed factual recipe/item/effect associations, not game code, sprites or serialized assets. All are read by the test suite; versioned filenames describe the inspected baseline and must not be renamed merely because the game updated.

| Fixture | Purpose / consumer |
| --- | --- |
| vanilla-biomes-1.0.20.json | Food/ingredient provenance; BiomeCatalogueTests |
| vanilla-equipment-1.0.20.json | Workbench/Forge equipment cases; EquipmentTests |
| vanilla-blackforge-1.0.20.json | Black Forge equipment cases; EquipmentTests |
| vanilla-galdr-1.0.20.json | Galdr equipment cases; EquipmentTests |
| native-equipment-icons-1.0.20.json | Native icon identities; EquipmentTests |
| vanilla-mead-1.0.20.json | Fermentation/effect categories; MeadTests |
| food-station-moves-1.0.21.json | Three food moves and identity compatibility; Compatibility121Tests |

The 1.0.21 audit checked relevant changes rather than regenerating unchanged baselines. Installed-API contracts supplement fixtures. Updating these requires new local assembly/asset evidence and deliberate expected-result review; blindly regenerating expected outputs from the classifier would weaken the tests. One-off historical inspection tools are privately archived and are not needed to build or run these tests.
