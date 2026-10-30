using System;
using System.Linq;
using System.Collections.Generic;
using CraftyQoL.Core;
using Mono.Cecil;

internal static class Program
{
    private static int _passed, _failed;
    private static void Check(string name, Action test)
    {
        try { test(); _passed++; }
        catch (Exception e) { _failed++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
    }
    internal static void Assert(bool condition, string message = "Unexpected result")
    {
        if (!condition) throw new Exception(message);
    }
    private static int Main(string[] args)
    {
        int rowWidgets=Array.IndexOf(args,"--row-widgets");
        if(rowWidgets>=0)
        {
            RowWidgetTests.Run(Check);
            Check("compiled row reuse lifecycle/input/presentation-only contracts",()=>RowWidgetTests.Contracts(args[rowWidgets+1]));
            Console.WriteLine($"{_passed} row-widget checks passed; {_failed} failed. No Unity runtime validation performed.");
            return _failed==0?0:1;
        }
        int profile=Array.IndexOf(args,"--opening-profile");
        if(profile>=0)
        {
            OpeningProfileTests.Run(Check);
            Check("optional profiler installed API/read-only/opt-in contracts",()=>OpeningProfileTests.Contracts(args[profile+1],args[profile+2],args[profile+3]));
            Console.WriteLine($"{_passed} opening profile checks passed; {_failed} failed. No Unity runtime profiling performed.");
            return _failed==0?0:1;
        }
        int storageApi=Array.IndexOf(args,"--storage-api");
        if(storageApi>=0)
        {
            Check("native storage authority/serialisation contracts",()=>StorageContracts.Native(args[storageApi+1]));
            Check("compiled storage controlled-write contracts",()=>StorageContracts.Plugin(args[storageApi+2]));
            Console.WriteLine($"{_passed} storage contracts passed; {_failed} failed. No game code executed.");return _failed==0?0:1;
        }
        if(args.Contains("--storage-only"))
        {StorageTests.Run(Check);Console.WriteLine($"{_passed} storage checks passed; {_failed} failed. No game code executed.");return _failed==0?0:1;}
        if(args.Contains("--inventory-only"))
        {InventoryTests.Run(Check);Console.WriteLine($"{_passed} inventory checks passed; {_failed} failed. No game code executed.");return _failed==0?0:1;}
        if(args.Contains("--biome-ui-only"))
        {BiomeAccessTests.Run(Check);Console.WriteLine($"{_passed} biome disclosure checks passed; {_failed} failed. No game code executed.");return _failed==0?0:1;}
        if(args.Contains("--visual-only"))
        {VisualStyleTests.Run(Check);ToolbarGeometryTests.Run(Check);Console.WriteLine($"{_passed} visual/geometry checks passed; {_failed} failed. No Unity code executed.");return _failed==0?0:1;}
        if(args.Contains("--compatibility-121"))
        {Compatibility121Tests.Run(Check);PreferenceTests.Run(Check);TrackerTests.Run(Check);Console.WriteLine($"{_passed} targeted compatibility/persistence checks passed; {_failed} failed. No game code executed.");return _failed==0?0:1;}
        if (args.Contains("--mead-only"))
        { MeadTests.Run(Check); Console.WriteLine($"{_passed} Mead/tracker refinement checks passed; {_failed} failed."); return _failed == 0 ? 0 : 1; }
        if (args.Contains("--tracker-only"))
        { TrackerTests.Run(Check); Console.WriteLine($"{_passed} tracker checks passed; {_failed} failed."); return _failed == 0 ? 0 : 1; }
        int trackerApi = Array.IndexOf(args,"--tracker-api");
        if(trackerApi>=0)
        { Check("current tracker/native contracts",()=>InspectApi(args[trackerApi+1]));Console.WriteLine($"{_passed} API checks passed; {_failed} failed.");return _failed==0?0:1; }
        int trackerPlugin = Array.IndexOf(args,"--tracker-plugin");
        if (trackerPlugin >= 0)
        { Check("tracker compiled read-only/lifecycle contracts",()=>InspectTracker(args[trackerPlugin+1])); Console.WriteLine($"{_passed} tracker integration checks passed; {_failed} failed."); return _failed==0?0:1; }
        int equipmentAudit = Array.IndexOf(args, "--equipment-audit");
        if (equipmentAudit >= 0) { EquipmentTests.Report(args[equipmentAudit + 1]); return 0; }
        if (args.Contains("--equipment-only"))
        {
            EquipmentTests.Run(Check);
            Console.WriteLine($"{_passed} equipment checks passed; {_failed} failed. No game code executed.");
            return _failed == 0 ? 0 : 1;
        }
        int tooltip = Array.IndexOf(args, "--tooltip-only");
        if (tooltip >= 0)
        {
            if (tooltip + 2 >= args.Length) throw new ArgumentException("--tooltip-only requires assembly_guiutils and plugin assembly paths");
            Check("native shared tooltip instance contract", () =>
            {
                using var assembly = AssemblyDefinition.ReadAssembly(args[tooltip + 1]);
                var type = assembly.MainModule.GetType("UITooltip") ?? throw new Exception("UITooltip missing; pass assembly_guiutils.dll");
                foreach (var expected in new[] { ("m_current", "UITooltip"), ("m_tooltip", "UnityEngine.GameObject") })
                {
                    var field = type.Fields.Single(f => f.Name == expected.Item1);
                    Assert(field.IsStatic && field.IsPrivate && field.FieldType.FullName == expected.Item2);
                    Assert(type.Methods.Single(m => m.Name == "OnHoverStart").Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == expected.Item1));
                }
            });
            Check("unchanged patch scope and discovery", () => InspectPlugin(args[tooltip + 2]));
            Console.WriteLine($"{_passed} tooltip integration checks passed; {_failed} failed. No game code executed.");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--geometry-only"))
        {
            ToolbarGeometryTests.Run(Check);
            Console.WriteLine($"{_passed} geometry checks passed; {_failed} failed. No game code executed.");
            return _failed == 0 ? 0 : 1;
        }
        int audit = Array.IndexOf(args, "--biome-audit");
        if (audit >= 0)
        {
            if (audit + 2 >= args.Length) throw new ArgumentException("--biome-audit requires input and output paths");
            BiomeAudit.Export(args[audit + 1], args[audit + 2]);
            return 0;
        }
        RowWidgetTests.Run(Check);
        OpeningProfileTests.Run(Check);
        Check("qualified stations only", () =>
        {
            Assert(StationRules.IsSupported("piece_cauldron"));
            Assert(StationRules.IsSupported("piece_preptable"));
            Assert(StationRules.IsSupported("piece_MeadCauldron") && StationRules.IsMead("piece_MeadCauldron"));
            Assert(StationRules.IsSupported("piece_workbench") && StationRules.IsSupported("forge") && StationRules.IsSupported("blackforge") && StationRules.IsSupported("piece_magetable"));
        });
        foreach (string? station in new[] { null, "", "piece_magetable_extra", "piece_meadcauldron", "piece_MeadCauldron(Clone)", "fermenter", "piece_oven", "$piece_cauldron", "Cauldron", "piece_cauldron(Clone)", "PIECE_CAULDRON", "mod_cauldron", "piece_cauldron_extra" })
        {
            string? candidate = station;
            Check("reject " + (candidate ?? "null"), () => Assert(!StationRules.IsSupported(candidate)));
        }
        int api = Array.IndexOf(args, "--api");
        if (api >= 0) Check("installed API signatures", () => { Assert(api + 1 < args.Length, "--api requires an assembly path"); InspectApi(args[api + 1]); });
        int plugin = Array.IndexOf(args, "--plugin");
        if (plugin >= 0) Check("plugin patch scope and read-only discovery adapter", () => { Assert(plugin + 1 < args.Length); InspectPlugin(args[plugin + 1]); });
        if (plugin >= 0) Check("compiled row reuse lifecycle/input/presentation-only contracts",()=>RowWidgetTests.Contracts(args[plugin+1]));
        if(api>=0)Check("native storage authority/serialisation contracts",()=>StorageContracts.Native(args[api+1]));
        if(plugin>=0)Check("compiled storage controlled-write contracts",()=>StorageContracts.Plugin(args[plugin+1]));
        foreach (string action in new[] { "Inventory", "Use", "JoyButtonY", "JoyButtonB", "JoyTabLeft", "JoyTabRight", "JoyDPadUp", "JoyLStickDown" })
        {
            string input = action;
            Check("inactive input unchanged: " + input, () => Assert(!InputPolicy.BlockAction(input, FocusMode.None)));
            Check("typing input gated: " + input, () => Assert(InputPolicy.BlockAction(input, FocusMode.Text)));
            Check("release latch gated: " + input, () => Assert(InputPolicy.BlockAction(input, FocusMode.Released)));
        }
        Check("toolbar group cycling remains native", () => Assert(!InputPolicy.BlockAction("JoyTabLeft", FocusMode.Toolbar)));
        Check("toolbar recipe navigation gated", () => Assert(InputPolicy.BlockAction("JoyDPadDown", FocusMode.Toolbar)));
        Check("unrelated actions remain native while typing", () => Assert(!InputPolicy.BlockAction("UnrelatedAction", FocusMode.Text)));
        StorageTests.Run(Check);
        InventoryTests.Run(Check);
        VisualStyleTests.Run(Check);
        Compatibility121Tests.Run(Check);
        MeadTests.Run(Check);
        TrackerTests.Run(Check);
        EquipmentTests.Run(Check);
        QueryTests.Run(Check);
        PreferenceTests.Run(Check);
        BiomeAccessTests.Run(Check);
        BiomeCatalogueTests.Run(Check);
        ToolbarGeometryTests.Run(Check);
        if (args.Contains("--benchmark")) Check("pure query performance targets", QueryBenchmark.Run);
        Console.WriteLine($"{_passed} checks passed; {_failed} failed. No game code executed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void InspectApi(string path)
    {
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path))
        {
            ModuleDefinition module = assembly.MainModule;
            TypeDefinition gui = module.GetType("InventoryGui") ?? throw new Exception("InventoryGui missing");
            Require(gui, "UpdateCraftingPanel", "System.Boolean");
            Require(gui, "UpdateRecipeList", "System.Collections.Generic.List`1<Recipe>");
            Require(gui, "SetRecipe", "System.Int32", "System.Boolean");
            Require(gui, "InCraftTab");
            var inventoryPlayer=module.GetType("Player");Require(inventoryPlayer,"NoCostCheat");
            Assert(inventoryPlayer.Methods.Single(m=>m.Name=="NoCostCheat").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="m_noPlacementCost"));
            var nativeAvailability=inventoryPlayer.Methods.Single(m=>m.Name=="GetAvailableRecipes");
            Assert(nativeAvailability.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="RequiredCraftingStation"));
            Assert(!nativeAvailability.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="m_debugMode"));
            Assert(module.GetType("Fermenter").Fields.Single(f=>f.Name=="m_conversion").IsPublic);
            Assert(module.GetType("SE_Stats").Fields.Single(f=>f.Name=="m_mods").IsPublic);
            var damageModifier=module.GetType("HitData").NestedTypes.Single(t=>t.Name=="DamageModifier");
            foreach(var expected in new[]{("Normal",0),("Resistant",1),("Weak",2),("Immune",3),("Ignore",4),("VeryResistant",5),("VeryWeak",6),("SlightlyResistant",7),("SlightlyWeak",8)})
                Assert(Convert.ToInt32(damageModifier.Fields.Single(f=>f.Name==expected.Item1).Constant)==expected.Item2,"Native resistance enum changed");
            foreach(string field in new[]{"m_healthUpFront","m_healthOverTime","m_healthPerTick","m_healthRegenMultiplier","m_staminaUpFront","m_staminaOverTime","m_staminaRegenMultiplier","m_staminaDrainPerSec","m_runStaminaDrainModifier","m_jumpStaminaUseModifier","m_attackStaminaUseModifier","m_blockStaminaUseModifier","m_blockStaminaUseFlatValue","m_dodgeStaminaUseModifier","m_swimStaminaUseModifier","m_homeItemStaminaUseModifier","m_sneakStaminaUseModifier","m_runStaminaUseModifier","m_windRunStaminaModifier","m_eitrUpFront","m_eitrOverTime","m_eitrRegenMultiplier"})
                Assert(module.GetType("SE_Stats").Fields.Single(f=>f.Name==field).IsPublic,"Missing Mead effect field "+field);
            foreach (string tab in new[] { "OnTabCraftPressed", "OnTabUpgradePressed" })
            {
                var instructions = gui.Methods.Single(m => m.Name == tab).Body.Instructions;
                int refreshAt = instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "UpdateCraftingPanel");
                int lastTabState = instructions.ToList().FindLastIndex(i => i.Operand is MethodReference m && m.Name == "set_interactable");
                Assert(lastTabState >= 0 && refreshAt > lastTabState, "Native tab state must precede panel prefix");
            }
            var itemData = module.GetType("ItemDrop").NestedTypes.Single(t => t.Name == "ItemData");
            var shared = itemData.NestedTypes.Single(t => t.Name == "SharedData");
            foreach (string field in new[] { "m_itemType", "m_skillType", "m_damages", "m_attack", "m_eitrRegenModifier" })
                Assert(shared.Fields.Any(f => f.Name == field && f.IsPublic), "Equipment reader missing public field " + field);
            var types = itemData.NestedTypes.Single(t => t.Name == "ItemType");
            foreach (var expected in new[] { ("OneHandedWeapon",3), ("Bow",4), ("Shield",5), ("Helmet",6), ("Chest",7), ("Ammo",9), ("Legs",11), ("Hands",12), ("TwoHandedWeapon",14), ("Shoulder",17), ("Tool",19), ("Torch",15), ("Utility",18), ("Trinket",24), ("TwoHandedWeaponLeft",22), ("AmmoNonEquipable",23) })
                Assert(Convert.ToInt32(types.Fields.Single(f => f.Name == expected.Item1).Constant) == expected.Item2, "Equipment ItemType contract changed");
            var skills = module.GetType("Skills").NestedTypes.Single(t => t.Name == "SkillType");
            foreach (var expected in new[] { ("Axes",7), ("ElementalMagic",9), ("BloodMagic",10), ("Pickaxes",12), ("Farming",106) })
                Assert(Convert.ToInt32(skills.Fields.Single(f => f.Name == expected.Item1).Constant) == expected.Item2, "Equipment skill contract changed");
            Require(gui, "Update");
            Require(gui, "Hide");
            Require(gui, "OnDestroy");
            TypeDefinition pair = gui.NestedTypes.Single(x => x.Name == "RecipeDataPair");
            Assert(pair.IsValueType, "Recipe pair no longer struct");
            Assert(pair.Properties.Single(x => x.Name == "Recipe").PropertyType.FullName == "Recipe");
            Assert(pair.Properties.Single(x => x.Name == "InterfaceElement").PropertyType.FullName == "UnityEngine.GameObject");
            Require(module.GetType("PlayerController"), "TakeInput", "System.Boolean");
            Require(module.GetType("GameCamera"),"UpdateMouseCapture");
            Require(module.GetType("Inventory"),"CountItems","System.String","System.Int32","System.Boolean");
            Assert(module.GetType("Inventory").Fields.Single(f=>f.Name=="m_onChanged").IsPublic);
            Require(module.GetType("PlayerProfile"),"GetPlayerID");
            Require(module.GetType("ZNet"),"GetWorldUID");
            Require(module.GetType("Player"),"KnowStationLevel","System.String","System.Int32");
            var stationKnowledge=module.GetType("Player").Methods.Single(m=>m.Name=="KnowStationLevel");
            Assert(stationKnowledge.IsPrivate && stationKnowledge.ReturnType.FullName=="System.Boolean");
            Assert(stationKnowledge.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="m_knownStations"));
            Assert(!stationKnowledge.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld),"Native station knowledge must remain read-only");
            Require(module.GetType("Player"),"IsRecipeKnown","System.String");
            Require(module.GetType("Player"),"IsKnownMaterial","System.String");
            Require(module.GetType("Player"),"TakeInput");
            TypeDefinition player = module.GetType("Player");
            Require(player, "IsBiomeKnown", "BiomeSector");
            var discovery = player.Methods.Single(m => m.Name == "IsBiomeKnown");
            Assert(discovery.IsPublic && discovery.ReturnType.FullName == "System.Boolean");
            Assert(player.Fields.Single(f => f.Name == "m_knownBiome").FieldType.FullName == "System.Collections.Generic.HashSet`1<System.String>");
            Assert(discovery.Body.Instructions.Any(i => i.Operand is FieldReference field && field.Name == "m_knownBiome"));
            Assert(discovery.Body.Instructions.Any(i => i.Operand is MethodReference method && method.Name == "GetName"));
            Assert(discovery.Body.Instructions.Any(i => i.Operand is MethodReference method && method.Name == "Contains"));
            foreach (string lifecycle in new[] { "Save", "Load" })
                Assert(player.Methods.Where(m => m.Name == lifecycle && m.HasBody).Any(m => m.Body.Instructions.Any(i => i.Operand is FieldReference field && field.Name == "m_knownBiome")));
            Require(module.GetType("BiomeSector"), "GetBiomeName", "Heightmap/Biome");
            Require(module.GetType("BiomeSector"), ".ctor", "AltBiomeWorldData", "Heightmap/Biome");
            Require(module.GetType("UIGamePad"), "ButtonPressed");
            Assert(gui.Fields.Single(x=>x.Name=="m_dragGo").FieldType.FullName=="UnityEngine.GameObject");
            Assert(gui.Fields.Single(x=>x.Name=="m_dragGo").IsPrivate);
            Assert(gui.Fields.Single(x => x.Name == "m_craftTimer").FieldType.FullName == "System.Single");
            Assert(gui.Fields.Single(x => x.Name == "m_recipeListBaseSize").FieldType.FullName == "System.Single");
        }
    }
    private static void InspectPlugin(string path)
    {
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path))
        {
            var module=assembly.MainModule;
            Assert(assembly.Name.Name=="CraftyQoL" && assembly.Name.Version==new Version(0,18,2,0),"Rebranded assembly identity mismatch");
            var identity=module.GetType("CraftyQoL.Plugin");
            Assert((string)identity.Fields.Single(f=>f.Name=="Id").Constant=="io.github.zinnkio.craftyqol" && (string)identity.Fields.Single(f=>f.Name=="Name").Constant=="CraftyQoL","Rebranded plugin/config identity mismatch");
            var declared=identity.CustomAttributes.Single(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin").ConstructorArguments;
            Assert((string)declared[0].Value=="io.github.zinnkio.craftyqol" && (string)declared[1].Value=="CraftyQoL" && (string)declared[2].Value=="0.18.3","BepInEx identity metadata mismatch");
            Assert(module.Types.All(t=>t.Namespace=="" || t.Namespace=="System.Runtime.CompilerServices" || t.Namespace=="Microsoft.CodeAnalysis" || t.Namespace=="CraftyQoL" || t.Namespace.StartsWith("CraftyQoL.",StringComparison.Ordinal)),"Unexpected runtime namespace");
            var allowed = new HashSet<string>(new[] { "InventoryGui.UpdateCraftingPanel", "InventoryGui.UpdateRecipeList", "InventoryGui.Update", "InventoryGui.Hide", "InventoryGui.OnDestroy",
                "ZInput.GetButtonDown", "ZInput.GetKeyDown", "UIGamePad.ButtonPressed", "PlayerController.TakeInput" }, StringComparer.Ordinal);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (TypeDefinition type in assembly.MainModule.Types)
                foreach (CustomAttribute patch in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
                    actual.Add(((TypeReference)patch.ConstructorArguments[0].Value).FullName + "." + (string)patch.ConstructorArguments[1].Value);
            Assert(actual.SetEquals(allowed), "Unexpected Harmony targets or missing core scope guards");
            TypeDefinition discovery = assembly.MainModule.GetType("CraftyQoL.BiomeDiscovery");
            var calls = discovery.Methods.Single(m => m.Name == "Read").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
            Assert(calls.Any(m => m.DeclaringType.FullName == "Player" && m.Name == "IsBiomeKnown"));
            Assert(calls.Where(m => m.DeclaringType.FullName == "Player").All(m => m.Name == "IsBiomeKnown"), "Discovery must not invoke player mutations or availability proxies");
            TypeDefinition scope = assembly.MainModule.GetType("CraftyQoL.StationScope");
            var scopeCalls = scope.Methods.Single(m => m.Name == "Identify").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
            Assert(scopeCalls.Any(m => m.Name == "InCraftTab") && scopeCalls.Any(m => m.Name == "IsActive"), "Live station scope must use native Craft state");
            TypeDefinition toolbar = assembly.MainModule.GetType("CraftyQoL.NativeToolbar");
            var toggleCalls = toolbar.Methods.Single(m => m.Name == "TogglePalette").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
            Assert(toggleCalls.Any(m => m.Name == "ApplyReservation") && toggleCalls.Any(m => m.Name == "WireNavigation"));
            Assert(!toggleCalls.Any(m => m.Name == "Invoke" || m.Name == "Filter" || m.Name == "UpdateCraftingPanel" || m.Name == "MakeButton"), "Palette disclosure must not query/rebuild recipes/controls");
            TypeDefinition icons = assembly.MainModule.GetType("CraftyQoL.EquipmentCategoryIcons");
            var iconCalls = icons.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>().ToArray();
            Assert(iconCalls.Any(m => m.Name == "GetItemPrefab") && iconCalls.Any(m => m.Name == "GetIcon"), "Category sprites must use native item APIs");
            Assert(!iconCalls.Any(m => m.Name == "FindObjectsOfTypeAll" || m.Name == "LoadImage"), "Category icons must not scan global resources or import textures");
            var rows = assembly.MainModule.GetType("CraftyQoL.RowDecoration");
            var rowTooltip=rows.Methods.Single(m=>m.Name=="Tooltip").Body.Instructions;
            Assert(!rowTooltip.Any(i=>i.Operand is FieldReference f && f.DeclaringType.FullName=="CraftyQoL.Core.RecipeInfo" && f.Name=="Biome"),"Recipe tooltips must not access biome classification");
            Assert(!rowTooltip.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="BiomeDiscovery" || i.Operand is string text && text.IndexOf("biome",StringComparison.OrdinalIgnoreCase)>=0),"Recipe tooltip biome disclosure remains");
            var paletteOps=toolbar.Methods.Concat(toolbar.NestedTypes.SelectMany(t=>t.Methods)).Where(m=>m.HasBody && (m.Name=="RebuildPalette" || m.Name.StartsWith("<RebuildPalette>",StringComparison.Ordinal))).SelectMany(m=>m.Body.Instructions);
            Assert(paletteOps.Any(i=>i.Operand is MethodReference m && m.Name=="IsDiscovered"),"Biome names/tooltips must require explicit discovery");

            Assert(!rows.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is string text && text == "~ "), "Estimated names must not acquire a tilde prefix");
            var rounded=assembly.MainModule.GetType("CraftyQoL.RoundedImage");
            Assert(rounded.BaseType.FullName=="UnityEngine.UI.BaseMeshEffect" && !rounded.Methods.Any(m=>m.Name=="Update" || m.Name=="LateUpdate"));
            Assert(rounded.Methods.Single(m=>m.Name=="ModifyMesh").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="ClipTriangle"));
            Assert(!rounded.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && (m.Name=="set_sprite" || m.Name=="set_material")),"Rounding must retain native sprite/material");
            var tooltipStyle=assembly.MainModule.GetType("CraftyQoL.TooltipBackground");
            Assert(tooltipStyle.Methods.Single(m=>m.Name=="Restore").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_richText"));
            foreach(string stat in new[]{"Health","Stamina","Eitr"})Assert(rowTooltip.Any(i=>i.Operand is FieldReference f && f.Name==stat),"Missing stat palette "+stat);
            var view = assembly.MainModule.GetType("CraftyQoL.FoodView");
            Assert(view.Methods.Single(m=>m.Name=="Suspend").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="CraftyQoL.CraftButtonStyle" && m.Name=="Dispose"),"Native controls must restore on Upgrade/unsupported/close");
            Assert(view.Methods.Single(m => m.Name == "get_Focus").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "InCraftTab"));
            Assert(view.Methods.Single(m => m.Name == "Suspend").Body.Instructions.Any(i => i.Operand is MethodReference m && m.Name == "Show"));
            Assert(view.Methods.Single(m=>m.Name=="Changed").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SameQuery"),"UI refresh must use the tested complete query comparison");
            var hide=assembly.MainModule.GetType("CraftyQoL.HidePatch").Methods.Single(m=>m.Name=="Postfix");
            var hideCalls=hide.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
            Assert(hideCalls.Any(m=>m.Name=="BeginClose") && !hideCalls.Any(m=>m.Name=="Suspend"),"Close must retain owned visuals through the native animation");
            var animation=assembly.MainModule.GetType("CraftyQoL.InventoryPresentation");
            var animationCalls=animation.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
            Assert(animationCalls.Any(m=>m.Name=="HasState") && animationCalls.Any(m=>m.Name=="GetCurrentAnimatorStateInfo"),"Closing continuation must qualify and observe the native hidden state");
            Assert(!animationCalls.Any(m=>m.Name=="SetBool" || m.Name=="SetTrigger" || m.Name=="Play" || m.Name=="set_speed"),"Inventory animation must remain vanilla-controlled");
            Assert(view.Methods.Single(m=>m.Name=="BeginClose").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetInteraction"));
            Assert(view.Methods.Single(m=>m.Name=="Update").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="FinishedClosing"));
            InspectTracker(path);
            Assert(!assembly.MainModule.AssemblyReferences.Any(r => r.Name == "Mono.Cecil" || r.Name == "ICSharpCode.Decompiler"), "Inspection tools leaked into plugin dependencies");
        }
    }
    private static void InspectTracker(string path)
    {
        using var assembly=AssemblyDefinition.ReadAssembly(path);
        var module=assembly.MainModule;
        var tracker=module.GetType("CraftyQoL.RecipeTracker");
        var catalogue=module.GetType("CraftyQoL.TrackerCatalogue");
        var calls=catalogue.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(calls.Any(m=>m.Name=="IsRecipeKnown") && calls.Any(m=>m.Name=="IsKnownMaterial") && catalogue.Methods.Single(m=>m.Name=="BindStationKnown").Body.Instructions.Any(i=>i.Operand is string text && text=="KnowStationLevel"),"Restoration must confirm current-character knowledge");
        Assert(!calls.Any(m=>m.Name=="HaveRequirements" || m.Name=="GetFirstRequiredItem" || m.Name=="ConsumeResources" || m.Name=="RemoveItem"),"Tracker must not mutate gameplay or depend on active station for restored availability");
        var prepare=catalogue.Methods.Single(m=>m.Name=="Prepare");
        Assert(prepare.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="GetAvailableRecipes"));
        Assert(prepare.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="CreativeKnowledge"));
        Assert(!catalogue.Methods.Where(m=>m.HasBody && m.Name!="Prepare").SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name=="GetAvailableRecipes"),"Native creative availability must be cached once, never queried per pin");
        Assert(module.GetType("CraftyQoL.FoodView").Methods.Single(m=>m.Name=="CanPinInventory").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="_sourceAccess"),"New inventory pins must match current native source/access state");
        var access=module.GetType("CraftyQoL.InventoryCrafting");
        var accessOps=access.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).ToArray();
        Assert(accessOps.Any(i=>i.Operand is MethodReference m && m.Name=="NoCostCheat") && !accessOps.Any(i=>i.Operand is FieldReference f && f.Name=="m_debugMode"));
        Assert(!accessOps.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld || i.OpCode.Code==Mono.Cecil.Cil.Code.Stsfld),"Craft access flags must be read-only");
        var writes=tracker.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld).Select(i=>i.Operand).OfType<FieldReference>().ToArray();
        Assert(writes.Any(f=>f.Name=="m_onChanged") && writes.Where(f=>f.DeclaringType.FullName=="Inventory").All(f=>f.Name=="m_onChanged"),"Only subscribe/unsubscribe the inventory Action");
        var all=tracker.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(all.Any(m=>m.Name=="CountItems") && !all.Any(m=>m.DeclaringType.FullName=="Container"),"Only carried inventory counts");
        Assert(all.Any(m=>m.Name=="GetWorldUID") && all.Any(m=>m.Name=="GetPlayerID"),"Tracker persistence scope must include character and world");
        var hud=module.GetType("CraftyQoL.TrackerHud");
        Assert(hud.Methods.Single(m=>m.Name=="BindVisible").HasBody && hud.Methods.Single(m=>m.Name=="Show").HasBody);
        var hudCalls=hud.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(hudCalls.Any(m=>m.DeclaringType.FullName=="CraftyQoL.Core.TrackerText" && m.Name=="Quantity"));
        Assert(hudCalls.Any(m=>m.DeclaringType.FullName=="CraftyQoL.Core.TrackerHudGeometry"),"HUD must use tested content geometry");
        Assert(!hud.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string s && (s=="Have " || s=="  Need " || s=="  Left ")),"Verbose material quantities remain");
        var mead=module.GetType("CraftyQoL.MeadMetadata");
        Assert(!mead.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is FieldReference f && (f.Name=="m_food" || f.Name=="m_foodStamina" || f.Name=="m_foodEitr" || f.Name=="m_name")),"Potion classification must not use food nutrition or names");
        Assert(all.Any(m=>m.Name=="CanEdit"),"Inventory must own tracker interaction");
        Assert(!tracker.Fields.Any(f=>f.Name=="_hidden") && !module.GetType("CraftyQoL.Plugin").Fields.Any(f=>f.Name=="TrackerEditKey"),"Obsolete F8/session hide state remains");
        Assert(hudCalls.Any(m=>m.Name=="set_sortingOrder") && hudCalls.Any(m=>m.Name=="set_blocksRaycasts"),"Tracker must layer and gate input with inventory");
        var surface=hud.Methods.Single(m=>m.Name=="SetSurface").Body.Instructions;
        Assert(surface.Any(i=>i.Operand is FieldReference f && f.Name=="_inventoryCanvas") && surface.Any(i=>i.Operand is FieldReference f && f.Name=="_parent") &&
            surface.Any(i=>i.Operand is MethodReference m && m.Name=="SetParent"),"Interactive tracker must share the inventory hierarchy and return to HUD");
        Assert(surface.Any(i=>i.Operand is MethodReference m && m.Name=="get_lossyScale") && surface.Any(i=>i.Operand is MethodReference m && m.Name=="set_localScale"),"Canvas changes must preserve HUD scale");
        Assert(hud.Methods.Single(m=>m.Name=="Show").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetSurface"),"Live presentation must reconcile input host");
        Assert(hud.Methods.Single(m=>m.Name=="Layout").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="TransformPoint") &&
            hud.Methods.Single(m=>m.Name=="BeginDrag").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="InverseTransformPoint"),"Saved position and drag must use the original HUD coordinates");
        Assert(hudCalls.Any(m=>m.Name=="set_ignoreParentGroups"),"Tracker's own interaction gate must be authoritative");

        Assert(!tracker.Methods.Single(m=>m.Name=="CanShow").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="InventoryGui"),"Inventory/chest/station UI must not gate tracker visibility");
        Assert(tracker.Methods.Single(m=>m.Name=="Visible").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="KeepVisible"),"Persistent pin must control gameplay visibility");
        Assert(!tracker.Methods.Single(m=>m.Name=="ToggleKeepVisible").Body.Instructions.Any(i=>i.Operand is MethodReference m && (m.Name=="RefreshModel" || m.Name=="Clear")),"Display pin must not rebuild recipe requirements");
        Assert(!tracker.Methods.Any(m=>m.Name=="Move") && !module.GetType("CraftyQoL.Core.TrackerStore").Methods.Any(m=>m.Name=="Move"),"Obsolete reorder code remains");
        Assert(!module.GetType("CraftyQoL.TrackerHudRow").Fields.Any(f=>f.Name=="Up" || f.Name=="Down"),"Reorder row actions remain");
        Assert(module.GetType("CraftyQoL.TrackerHideGraphic")==null,"Obsolete F8 eye control remains");
        var approval=module.GetType("CraftyQoL.RecipeMoveContext").Methods.Single(m=>m.Name=="Approved");
        var approvalCalls=approval.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(approvalCalls.Any(m=>m.Name=="Approves") && approvalCalls.Any(m=>m.Name=="Fingerprint"),"Moves must verify the exact live recipe structure");
        foreach(var owner in new[]{("CraftyQoL.RecipeMetadata","Ensure"),("CraftyQoL.RecipeTracker","RefreshModel")})
            Assert(module.GetType(owner.Item1).Methods.Single(m=>m.Name==owner.Item2).Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="MigrateLoadedRecipes"),"Migration missing from independent crafting/tracker lifecycle");
        var pin=module.GetType("CraftyQoL.TrackerPinControl");
        Assert(pin.Methods.Single(m=>m.Name=="Hide").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="RestoreLayout"));
        Assert(pin.Methods.Single(m=>m.Name=="ApplyLayout").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_offsetMax"));
        var pinHeight=pin.Methods.Single(m=>m.Name=="MatchCraftHeight").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Select(m=>m.Name).ToArray();
        foreach(string field in new[]{"anchorMin","anchorMax","pivot","offsetMin","offsetMax"})
            Assert(pinHeight.Contains("get_"+field) && pinHeight.Contains("set_"+field),"Pin must match native Craft vertical geometry: "+field);
        Assert(pin.Methods.Single(m=>m.Name=="ApplyLayout").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="MatchCraftHeight"),"Pin height must reconcile on reopen");
        Assert(module.GetType("CraftyQoL.PinGraphic").BaseType.FullName=="UnityEngine.UI.MaskableGraphic","Pin must be a mesh icon, not a font glyph");
        Assert(!pin.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name=="set_text"),"Pin label must not reappear");
        Assert(pin.Methods.Single(m=>m.Name=="Toggle").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Identify"),"Detail action must recheck Craft scope");
    }
    private static void Require(TypeDefinition type, string name, params string[] parameters)
    {
        Assert(type.Methods.Any(m => m.Name == name && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)), type.Name + "." + name + " signature missing");
    }
}
