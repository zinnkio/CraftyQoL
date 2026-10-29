# Building CraftyQoL

## Prerequisites

- Windows/PowerShell is the qualified workflow.
- [Microsoft .NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) **10.0.402**, selected by `global.json`. This pins the qualified toolchain, not the newest SDK.
- A legitimate local **Valheim 1.0.21** installation and its shipped Managed assemblies.
- Separately installed **BepInExPack_Valheim 5.4.2351**; core directory containing BepInEx, Harmony and test-only Mono.Cecil (qualified assembly versions 5.4.23.5, 2.9.0.0, 0.10.4.0).
- NuGet connectivity for SDK framework reference packs on a clean machine. No game files are downloaded by this project.

Copy `Environment.props.example` to `Environment.props`, replace the placeholder, and adjust `BepInExCore` if the pack is elsewhere. This file is ignored; never commit machine paths. `Directory.Build.targets` keeps runtime references non-copy-local. Plugin targets net472; tests target net10.0 and link pure core source directly. Mono.Cecil is test-only.

## Build

```powershell
.\tools\dotnet.ps1 build CraftyQoL.sln -c Release --nologo
```

The wrapper uses `.tools/dotnet/dotnet.exe` if supplied locally, otherwise installed `dotnet`; it changes only process-local CLI/cache settings. It never installs an SDK or changes system PATH.

Output: `src/CraftyQoL/bin/Release/net472/CraftyQoL.dll`. PDB is development output, not required for installation. Install manually only with Valheim closed; see [installation](INSTALLATION.md).

## Checks

```powershell
# Pure query, metadata, persistence, geometry and transaction tests
.\tools\dotnet.ps1 run --project tests/CraftyQoL.Tests -c Release --no-build

# Full pure + installed API + compiled-plugin contracts
$game = 'REPLACE_WITH_YOUR_VALHEIM_INSTALL'
$managed = Join-Path $game 'valheim_Data/Managed'
$plugin = 'src/CraftyQoL/bin/Release/net472/CraftyQoL.dll'
.\tools\dotnet.ps1 run --project tests/CraftyQoL.Tests -c Release --no-build -- --api (Join-Path $managed 'assembly_valheim.dll') --plugin $plugin
.\tools\dotnet.ps1 run --project tests/CraftyQoL.Tests -c Release --no-build -- --tooltip-only (Join-Path $managed 'assembly_guiutils.dll') $plugin
```

API checks inspect local assemblies without loading the game. Compiled contracts enforce identity, patch scope, discovery, tracker isolation and storage authority/controlled writes. Factual JSON fixtures contain reviewed recipe associations/equipment/effects and1.0.21 food moves, not game assemblies, sprites or serialized asset bundles.

Targeted switches: `--tracker-only`, `--inventory-only`, `--mead-only`, `--equipment-only`, `--storage-only`, `--visual-only`. Optional `--benchmark` is a synthetic pure-query microbenchmark, not Unity profiling/multiplayer testing. Machine timings are not game frame costs.

## Package

From a clean public checkout with ignored reference configuration:

```powershell
.\tools\package.ps1
```

The helper builds, runs full API/compiled and tooltip checks, and creates a version/commit-named local preview under ignored `artifacts/`. Includes only DLL, installation/configuration/manual-test notes, licence/notices, changelog and provenance. Refuses unexpected dependencies/unclean trees. No proprietary libraries/assets, PDBs, personal settings or saves. No install/deploy/launch/publication; existing artifacts preserved.

Binary byte reproducibility depends on SDK, source revision, references and build paths. Snapshot build verifies structural compatibility, not byte identity across arbitrary machines. [Manual checks](MANUAL_TESTS.md) remain necessary.
