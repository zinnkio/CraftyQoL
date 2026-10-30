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

API checks inspect local assemblies without loading the game. Compiled contracts enforce identity, patch scope, discovery, tracker isolation and storage authority/controlled writes. All seven factual JSON fixtures remain required; their [provenance and consumers](../tests/CraftyQoL.Tests/Data/README.md) explain the older version labels. No fixture contains game code, sprites or serialized asset bundles.

Targeted switches: `--tracker-only`, `--inventory-only`, `--mead-only`, `--equipment-only`, `--storage-only`, `--visual-only`. `--row-widgets <plugin>` checks reusable control contracts. `--opening-profile <assembly_valheim> <UnityEngine.UI> <plugin>` checks optional timing instrumentation. Optional `--benchmark` is a pure-query microbenchmark, not Unity profiling/multiplayer testing. Machine timings are not game frame costs.

Before committing public files, run `./tools/check-publication.ps1`. This source-only guard checks the allowlist, sensitive-content patterns, local Markdown links and ignore rules. GitHub also runs it on pushes/PRs. It supplements manual review: ignore rules cannot prevent deliberate force-adds, and no pattern scanner proves absence of every secret. Never push private development history.

## Package

From a clean public checkout with ignored reference configuration:

```powershell
.\tools\package.ps1
```

The helper checks source hygiene, builds Release without debug symbols, runs full API/compiled and tooltip checks, and writes an install ZIP, standalone DLL, SHA256SUMS.txt and developer build-info.json under ignored artifacts/CraftyQoL-<version>/. The ZIP has exactly four files: BepInEx/plugins/CraftyQoL/CraftyQoL.dll, README.md, LICENSE and THIRD_PARTY_NOTICES.md. The README is sourced from docs/RELEASE_INSTALL.md. Build provenance stays outside the player ZIP. No runtime dependency besides the separately supplied game/BepInEx is bundled.

Source paths are mapped to a neutral prefix; proprietary references/PDBs/settings are excluded. Packaging refuses dirty source trees or existing release output, never overwrites older artifacts, and never deploys, launches or publishes anything. Keep older builds outside the publication checkout before creating another package with the same version. A source-only cleanup does not change the plugin version or replace a published release.

Byte reproducibility depends on SDK, source revision and reference inputs. Successful checks supplement [manual validation](MANUAL_TESTS.md); they do not establish every runtime/mod/network combination.
