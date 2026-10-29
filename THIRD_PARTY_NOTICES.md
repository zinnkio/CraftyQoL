# Independent source and external dependencies

The original CraftyQoL source and documentation are under the accompanying MIT licence. That licence does not grant rights to Valheim, Unity, loader libraries, development tools or reference projects.

The test package contains only our compiled plugin and original documents. It contains no extracted game assets, game/Unity/BepInEx/Harmony assemblies or reference-mod source. Native fonts/sprites are referenced from the running game rather than redistributed.

Runtime dependencies are installed separately: Valheim and its Unity runtime; the denikson BepInExPack_Valheim pack and bundled HarmonyX. Their respective licences and upstream notices apply. Development-only tools include Microsoft's .NET SDK/reference packages, ILSpy, Mono.Cecil and UnityPy with its dependencies; these are not included in the plugin package. Qualified versions are in docs/BUILDING.md; dependencies/tools are acquired separately.

CraftIndex (jg224, MIT) and CraftSort (GrandpaUA, AGPL-3.0) were inspected as technical references for public game APIs and compatibility limitations. No source files, code fragments, custom icons or assets from either project were incorporated, and this project is not a fork or an adapter incorporating CraftSort. Upstream projects do not endorse this work.

The historical copyright notice in LICENSE is intentionally preserved. No new licence was selected. Mono.Cecil is test-only and not bundled in public source or the runtime plugin.
