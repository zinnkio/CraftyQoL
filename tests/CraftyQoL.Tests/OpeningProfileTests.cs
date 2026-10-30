using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CraftyQoL.Core;
using Mono.Cecil;

internal static class OpeningProfileTests
{
    private static OpeningCapture Capture(bool allocations=true)=>new OpeningCapture(1,100,1000,1000,allocations,true,1,481,2500,4,true);
    private static void TimingProbe(){}
    private static class OtherProbe { internal static void TimingProbe(){} }
    private static void NativePhases(OpeningCapture c)
    {foreach(var p in new[]{OpeningPhase.Show,OpeningPhase.PanelIncludingHooks,OpeningPhase.ListIncludingHooks})c.Add(p,100,1000,1001,0);}
    private static void IntegrationPhases(OpeningCapture c)
    {foreach(var p in new[]{OpeningPhase.ToolbarPresent,OpeningPhase.FilterTotal,OpeningPhase.MetadataEnsure,OpeningPhase.MetadataRecords,OpeningPhase.RowsTotal})c.Add(p,100,1000,1001,0);}
    internal static void Run(Action<string,Action> check)
    {
        check("opening profiler exact own prefix accepted despite distinct owner",()=>
        {
            const string main="io.github.zinnkio.craftyqol",owner=main+".opening-profile";
            var method=typeof(OpeningProfileTests).GetMethod(nameof(TimingProbe),BindingFlags.NonPublic|BindingFlags.Static)!;
            Program.Assert(owner!=main,"Reproduce the original owner-only conflict");
            Program.Assert(OpeningProbeIdentity.Matches(true,owner,method,owner,method));
        });
        check("opening profiler never trusts owner alone or lookalike prefix",()=>
        {
            const string owner="io.github.zinnkio.craftyqol.opening-profile";
            var method=typeof(OpeningProfileTests).GetMethod(nameof(TimingProbe),BindingFlags.NonPublic|BindingFlags.Static)!;
            var other=typeof(OtherProbe).GetMethod(nameof(TimingProbe),BindingFlags.NonPublic|BindingFlags.Static)!;
            Program.Assert(!OpeningProbeIdentity.Matches(true,owner+".other",method,owner,method));
            Program.Assert(!OpeningProbeIdentity.Matches(true,owner,other,owner,method));
            Program.Assert(!OpeningProbeIdentity.Matches(true,owner,null,owner,method));
        });
        check("opening profiler absent inactive or unresolved identity fails closed",()=>
        {
            var method=typeof(OpeningProfileTests).GetMethod(nameof(TimingProbe),BindingFlags.NonPublic|BindingFlags.Static)!;
            Program.Assert(!OpeningProbeIdentity.Matches(false,"own",method,"own",method));
            Program.Assert(!OpeningProbeIdentity.Matches(true,"own",method,"own",null));
        });
        check("opening requested-enabled but missing integration invalidates comparison",()=>
        {
            var c=Capture();NativePhases(c);
            Program.Assert(!c.CraftingIntegrationObserved && c.ComparisonIssue=="missing-enabled-crafting-integration");
            using var j=JsonDocument.Parse(c.Json());
            Program.Assert(j.RootElement.GetProperty("featuresEnabled").GetBoolean());
            Program.Assert(!j.RootElement.GetProperty("comparisonValid").GetBoolean());
            Program.Assert(j.RootElement.GetProperty("invalidReason").GetString()==c.ComparisonIssue);
        });
        check("opening observed full integration validates schema2 enabled capture",()=>
        {
            var c=Capture();NativePhases(c);IntegrationPhases(c);
            Program.Assert(c.CraftingIntegrationObserved && c.ComparisonIssue==null);
            using var j=JsonDocument.Parse(c.Json());
            Program.Assert(j.RootElement.GetProperty("schema").GetInt32()==2 && j.RootElement.GetProperty("comparisonValid").GetBoolean());
            Program.Assert(j.RootElement.GetProperty("invalidReason").ValueKind==JsonValueKind.Null);
        });
        check("opening disabled capture only valid without crafting integration",()=>
        {
            var c=new OpeningCapture(1,100,1000,1000,false,false,1,481,2500,4,true);NativePhases(c);
            Program.Assert(c.ComparisonIssue==null && !c.CraftingIntegrationObserved);
            c.Add(OpeningPhase.FilterTotal,100,1000,1001,0);
            Program.Assert(c.ComparisonIssue=="unexpected-crafting-integration-in-disabled-run");
        });
        check("opening incomplete noncraft and truncated captures cannot compare",()=>
        {
            var c=Capture();Program.Assert(c.ComparisonIssue=="missing-native-opening-phases");
            var upgrade=new OpeningCapture(1,100,1000,1000,false,true,1,481,2500,4,false);
            NativePhases(upgrade);IntegrationPhases(upgrade);Program.Assert(upgrade.ComparisonIssue=="not-craft-tab-at-entry");
            NativePhases(c);IntegrationPhases(c);
            for(int i=0;i<OpeningCapture.SampleLimit;i++)c.Add(OpeningPhase.Query,100,1000,1001,0);
            Program.Assert(c.ComparisonIssue=="truncated-timeline");
        });
        check("opening profile inclusive nested timings stay separate",()=>
        {
            var c=Capture();c.Add(OpeningPhase.Show,100,1000,1400,10000);
            c.Add(OpeningPhase.FilterTotal,100,1050,1150,2000);
            Program.Assert(c.Calls(OpeningPhase.Show)==1 && c.Ticks(OpeningPhase.Show)==400);
            Program.Assert(c.Ticks(OpeningPhase.FilterTotal)==100);
            using var j=JsonDocument.Parse(c.Json());
            Program.Assert(j.RootElement.GetProperty("phases")[0].GetProperty("inclusiveMs").GetDouble()==400);
        });
        check("opening profile unsupported allocation counter is null",()=>
        {
            var c=Capture(false);c.Add(OpeningPhase.MetadataEnsure,100,1000,1001,999);
            using var j=JsonDocument.Parse(c.Json());
            Program.Assert(!j.RootElement.GetProperty("allocationBytesAvailable").GetBoolean());
            Program.Assert(j.RootElement.GetProperty("phases")[0].GetProperty("allocatedBytes").ValueKind==JsonValueKind.Null);
        });
        check("opening profile preserves per-refresh input output and frame",()=>
        {
            var c=Capture();c.Add(OpeningPhase.ListIncludingHooks,102,1000,1200,8192,470,125);
            using var j=JsonDocument.Parse(c.Json());var v=j.RootElement.GetProperty("samples")[0];
            Program.Assert(v.GetProperty("inputCount").GetInt32()==470 && v.GetProperty("outputCount").GetInt32()==125);
            Program.Assert(v.GetProperty("frame").GetInt32()==102 && v.GetProperty("allocatedBytes").GetInt64()==8192);
        });
        check("opening profile sample overflow preserves aggregate totals",()=>
        {
            var c=Capture();for(int i=0;i<OpeningCapture.SampleLimit+5;i++)c.Add(OpeningPhase.PanelIncludingHooks,100,1000,1001,1);
            Program.Assert(c.Dropped==5 && c.Calls(OpeningPhase.PanelIncludingHooks)==261 && c.Ticks(OpeningPhase.PanelIncludingHooks)==261);
            using var j=JsonDocument.Parse(c.Json());Program.Assert(j.RootElement.GetProperty("samples").GetArrayLength()==256);
        });
        check("opening profile native rows aggregate without exhausting timeline",()=>
        {
            var c=Capture();for(int i=0;i<5000;i++)c.Add(OpeningPhase.NativeRowConstruction,100,1000,1002,32);
            Program.Assert(c.Calls(OpeningPhase.NativeRowConstruction)==5000 && c.Ticks(OpeningPhase.NativeRowConstruction)==10000 && c.Dropped==0);
            using var j=JsonDocument.Parse(c.Json());Program.Assert(j.RootElement.GetProperty("samples").GetArrayLength()==0);
        });
        check("opening profile invariant JSON and event counts",()=>
        {
            var prior=System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                var c=Capture();c.Note(OpeningCapture.Event.MetadataRebuild);c.Note(OpeningCapture.Event.ListInput,470);
                c.FrameInterval(99,3.5);using var j=JsonDocument.Parse(c.Json());
                Program.Assert(j.RootElement.GetProperty("events").GetProperty("MetadataRebuild").GetInt32()==1);
                Program.Assert(j.RootElement.GetProperty("frameStartIntervals")[0].GetProperty("ms").GetDouble()==3.5);
            }
            finally{System.Globalization.CultureInfo.CurrentCulture=prior;}
        });
        check("opening profile frame capture bounded and ignores invalid intervals",()=>
        {
            var c=Capture();c.FrameInterval(97,10);c.FrameInterval(100,double.NaN);c.FrameInterval(100,-1);
            for(int i=98;i<130;i++)c.FrameInterval(i,16);
            using var j=JsonDocument.Parse(c.Json());var frames=j.RootElement.GetProperty("frameStartIntervals");
            Program.Assert(frames.GetArrayLength()==16 && frames[0].GetProperty("precedingFrame").GetInt32()==98);
        });
        check("opening profile ignores invalid time and phase",()=>
        {
            var c=Capture();c.Add(OpeningPhase.Count,100,1000,1001,2);c.Add(OpeningPhase.Query,100,1001,1000,2);
            Program.Assert(c.Calls(OpeningPhase.Query)==0);
            using var j=JsonDocument.Parse(c.Json());Program.Assert(j.RootElement.GetProperty("phases").GetArrayLength()==0);
        });
    }
    internal static void Contracts(string game,string ui,string plugin)
    {
        using var g=AssemblyDefinition.ReadAssembly(game);using var u=AssemblyDefinition.ReadAssembly(ui);
        void Method(ModuleDefinition module,string type,string name,params string[] args)
        {
            Program.Assert(module.GetType(type).Methods.Any(m=>m.Name==name && m.ReturnType.FullName=="System.Void" &&
                m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(args)),type+"."+name+" changed");
        }
        Method(g.MainModule,"InventoryGui","Show","Container","System.Int32");
        Method(g.MainModule,"InventoryGui","UpdateCraftingPanel","System.Boolean");
        Method(g.MainModule,"InventoryGui","UpdateRecipeList","System.Collections.Generic.List`1<Recipe>");
        Method(g.MainModule,"InventoryGui","AddRecipeToList","Player","Recipe","ItemDrop/ItemData","System.Boolean");
        Method(g.MainModule,"Player","GetAvailableRecipes","System.Collections.Generic.List`1<Recipe>&");
        Method(u.MainModule,"UnityEngine.UI.CanvasUpdateRegistry","PerformUpdate");
        using var p=AssemblyDefinition.ReadAssembly(plugin);var t=p.MainModule.GetType("CraftyQoL.InventoryOpeningProfiler");
        Program.Assert(t!=null && !t.CustomAttributes.Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"));
        var init=t!.Methods.Single(m=>m.Name=="Initialise");
        Program.Assert(init.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.DeclaringType.FullName==t.FullName && m.Name=="Patch")==6);
        Program.Assert(init.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="CraftyQoL.Compatibility" && m.Name=="Conflict"),"Startup must check the diagnostic compatibility interaction");
        var patchSetup=t.Methods.Single(m=>m.Name=="Patch").Body.Instructions;
        Program.Assert(patchSetup.Any(i=>i.Operand is FieldReference f && f.Name=="_listPrefix" && i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld),"Trust only the registered list-prefix method");
        var own=t.Methods.Single(m=>m.Name=="IsOwnListPrefix").Body.Instructions;
        Program.Assert(own.Any(i=>i.Operand is MethodReference m && m.Name=="get_PatchMethod"));
        Program.Assert(own.Any(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="CraftyQoL.Core.OpeningProbeIdentity" && m.Name=="Matches"));
        var conflict=p.MainModule.GetType("CraftyQoL.Compatibility").Methods.Single(m=>m.Name=="Conflict").Body.Instructions;
        Program.Assert(conflict.Count(i=>i.Operand is MethodReference m && m.Name=="IsOwnListPrefix")==1,"Exact diagnostic exception must apply only once, to prefixes");
        Program.Assert(conflict.Any(i=>i.Operand as string=="Untested recipe-list prefix: ") && conflict.Any(i=>i.Operand as string=="Untested recipe-list transpiler: "),"Foreign compatibility guards retained");
        var awake=p.MainModule.GetType("CraftyQoL.Plugin").Methods.Single(m=>m.Name=="Awake");
        var code=awake.Body.Instructions;
        int profileKey=code.ToList().FindIndex(i=>i.Operand as string=="ProfileInventoryOpenings");
        Program.Assert(profileKey>=0 && code[profileKey+1].OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0,"Profiling must default off");
        Program.Assert(code.Any(i=>i.Operand is FieldReference f && f.Name=="_profileInventoryOpenings"));
        // Diagnostic bridge may read game data, but cannot mutate inventory/recipes/GUI fields.
        foreach(var method in t.Methods.Where(m=>m.HasBody))
        foreach(var instruction in method.Body.Instructions)
        if((instruction.OpCode.Code is Mono.Cecil.Cil.Code.Stfld or Mono.Cecil.Cil.Code.Stsfld) && instruction.Operand is FieldReference field)
            Program.Assert(field.DeclaringType.FullName.StartsWith("CraftyQoL.") ||
                field.DeclaringType.FullName=="HarmonyLib.HarmonyMethod" && field.Name=="priority","Foreign field write: "+field.FullName);
    }
}
