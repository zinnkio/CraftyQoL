using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using CraftyQoL.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Diagnostic hooks are installed manually only at startup when explicitly enabled.
    // No Harmony attributes: the normal PatchAll scope remains unchanged.
    internal sealed class InventoryOpeningProfiler : MonoBehaviour
    {
        private static InventoryOpeningProfiler? _instance;
        private Harmony? _patches;
        private MethodInfo? _listPrefix;
        private const string Owner=Plugin.Id+".opening-profile";
        private OpeningCapture? _capture;
        private Func<long>? _allocated;
        private string _path="";
        private int _id, _thread;
        private bool _failed;
        private readonly double[] _recent=new double[2];
        private int _recentFrame=-1;

        internal void Initialise()
        {
            try
            {
                _thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
                var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);
                if(method!=null)
                {
                    var read=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
                    try
                    {
                        long initial=read();var probe=new byte[128];GC.KeepAlive(probe);
                        if(read()>initial)_allocated=read;
                    }
                    catch(NotSupportedException){}catch(NotImplementedException){}
                }
                string folder=Path.Combine(Paths.ConfigPath,Plugin.Id,"diagnostics");
                Directory.CreateDirectory(folder);
                _path=Path.Combine(folder,"inventory-openings-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".jsonl");
                _patches=new Harmony(Owner);
                Patch(typeof(InventoryGui),"Show",new[]{typeof(Container),typeof(int)},nameof(ShowPrefix),nameof(Finish));
                Patch(typeof(InventoryGui),"UpdateCraftingPanel",new[]{typeof(bool)},nameof(PanelPrefix),nameof(Finish));
                Patch(typeof(InventoryGui),"UpdateRecipeList",new[]{typeof(List<Recipe>)},nameof(ListPrefix),nameof(ListFinish));
                Patch(typeof(InventoryGui),"AddRecipeToList",new[]{typeof(Player),typeof(Recipe),typeof(ItemDrop.ItemData),typeof(bool)},nameof(RowPrefix),nameof(Finish));
                Patch(typeof(Player),"GetAvailableRecipes",new[]{typeof(List<Recipe>).MakeByRefType()},nameof(AvailabilityPrefix),nameof(AvailabilityFinish));
                Patch(typeof(CanvasUpdateRegistry),"PerformUpdate",Type.EmptyTypes,nameof(CanvasPrefix),nameof(Finish));
                _instance=this;
                string? conflict=Compatibility.Conflict();
                Plugin.Instance?.DiagnosticMessage(conflict==null
                    ? "Inventory opening profiler compatibility self-check passed; exact timing hook recognised."
                    : "Inventory opening profiler: enabled comparison cannot run while a compatibility conflict exists: "+conflict);
                // Warm allocation/timer/capture formatting before the first measured opening.
                new OpeningCapture(0,0,Stopwatch.GetTimestamp(),Stopwatch.Frequency,_allocated!=null,false,0,0,0,0,true).Json();
                Plugin.Instance?.DiagnosticMessage("Inventory opening profiling enabled; writes after each capture window. File: "+_path+
                    "; thread-allocation counter "+(_allocated!=null?"available":"unavailable")+". Disabled-feature runs retain inactive hooks/BepInEx; not unmodded vanilla.");
            }
            catch(Exception error){Disable(error);}
        }
        private void Patch(Type type,string name,Type[] args,string prefix,string finalizer)
        {
            MethodInfo target=AccessTools.DeclaredMethod(type,name,args)??throw new MissingMethodException(type.FullName,name);
            var before=new HarmonyMethod(typeof(InventoryOpeningProfiler),prefix){priority=Priority.First};
            var after=new HarmonyMethod(typeof(InventoryOpeningProfiler),finalizer){priority=Priority.Last};
            _patches!.Patch(target,prefix:before,finalizer:after);
            if(target==GameContracts.RebuildList)_listPrefix=before.method;
        }
        internal static bool IsOwnListPrefix(Patch prefix)=>OpeningProbeIdentity.Matches(
            _instance!=null && !_instance._failed,prefix.owner,prefix.PatchMethod,Owner,_instance?._listPrefix);
        internal readonly struct Scope:IDisposable
        {
            private readonly OpeningCapture? _owner;
            private readonly OpeningPhase _phase;
            private readonly long _start,_bytes;
            private readonly int _frame,_input;
            internal Scope(OpeningCapture owner,OpeningPhase phase,long bytes,int input)
            {_owner=owner;_phase=phase;_bytes=bytes;_input=input;_frame=Time.frameCount;_start=Stopwatch.GetTimestamp();}
            public void Dispose()=>Complete(-1);
            internal void Complete(int output)
            {
                if(_owner==null)return;
                try
                {
                    var profiler=_instance;
                    if(profiler==null || profiler._failed || profiler._capture!=_owner || System.Threading.Thread.CurrentThread.ManagedThreadId!=profiler._thread)return;
                    long end=Stopwatch.GetTimestamp();
                    _owner.Add(_phase,_frame,_start,end,profiler._allocated!=null?profiler._allocated()-_bytes:0,_input,output);
                }
                catch(Exception error){_instance?.Disable(error);}
            }
        }
        internal static Scope Measure(OpeningPhase phase,int input=-1)
        {
            var p=_instance;
            if(p==null || p._failed || p._capture==null)return default;
            try
            {
                if(System.Threading.Thread.CurrentThread.ManagedThreadId!=p._thread)return default;
                return new Scope(p._capture,phase,p._allocated!=null?p._allocated():0,input);
            }
            catch(Exception error){p.Disable(error);return default;}
        }
        internal static void Note(OpeningCapture.Event kind,int amount=1)
        {
            var p=_instance;
            if(p==null || p._failed || p._capture==null)return;
            try{if(System.Threading.Thread.CurrentThread.ManagedThreadId==p._thread)p._capture.Note(kind,amount);}
            catch(Exception error){p.Disable(error);}
        }
        private static void ShowPrefix(InventoryGui __instance,Container __0,out Scope __state)
        {
            __state=default;var p=_instance;
            if(p==null || p._failed)return;
            try
            {
                var player=Player.m_localPlayer;
                if(__0 || !player || player.GetCurrentCraftingStation() || InventoryPresentation.Open)return;
                // If reopening before the previous window ended, keep that window honest: no disk IO here.
                if(p._capture!=null)return;
                var db=ObjectDB.instance;var scene=ZNetScene.instance;
                p._capture=new OpeningCapture(++p._id,Time.frameCount,Stopwatch.GetTimestamp(),Stopwatch.Frequency,p._allocated!=null,
                    Plugin.Instance!=null && Plugin.Instance.Enabled.Value,(int)InventoryCrafting.Access,
                    db?db.m_recipes.Count:0,scene?scene.m_prefabs.Count:0,RecipeTracker.ProfilePinCount,__instance.InCraftTab());
                if(p._recentFrame==Time.frameCount-1)
                {p._capture.FrameInterval(Time.frameCount-3,p._recent[0]);p._capture.FrameInterval(Time.frameCount-2,p._recent[1]);}
                __state=Measure(OpeningPhase.Show);
            }
            catch(Exception error){p.Disable(error);}
        }
        private static void PanelPrefix(out Scope __state)=>__state=Measure(OpeningPhase.PanelIncludingHooks);
        private static void CanvasPrefix(out Scope __state)=>__state=Measure(OpeningPhase.CanvasRebuild);
        private static void AvailabilityPrefix(out Scope __state)=>__state=Measure(OpeningPhase.Availability);
        private static void RowPrefix(out Scope __state)=>__state=Measure(OpeningPhase.NativeRowConstruction);
        private static void ListPrefix(List<Recipe> __0,out Scope __state)
        {__state=Measure(OpeningPhase.ListIncludingHooks,__0.Count);Note(OpeningCapture.Event.ListInput,__0.Count);}
        private static void Finish(Scope __state)=>__state.Dispose();
        private static void ListFinish(InventoryGui __instance,Scope __state)
        {
            if(_instance?._capture==null){__state.Dispose();return;}
            try
            {
                int count=((IList)GameContracts.Pairs.GetValue(__instance)).Count;
                __state.Complete(count);Note(OpeningCapture.Event.ListOutput,count);
            }
            catch(Exception error){_instance?.Disable(error);}
        }
        private static void AvailabilityFinish(List<Recipe> __0,Scope __state)
        {__state.Complete(__0!=null?__0.Count:-1);if(__0!=null)Note(OpeningCapture.Event.AvailabilityOutput,__0.Count);}
        private void Update()
        {
            if(_failed)return;
            try
            {
                double interval=Time.unscaledDeltaTime*1000.0;
                _recent[0]=_recent[1];_recent[1]=interval;_recentFrame=Time.frameCount;
                var capture=_capture;if(capture==null)return;
                capture.FrameInterval(Time.frameCount-1,interval);
                if(Time.frameCount<=capture.OpenFrame+10)return;
                _capture=null; // Formatting, file IO and log output are outside measured scopes/window.
                File.AppendAllText(_path,capture.Json()+Environment.NewLine,new UTF8Encoding(false));
                Plugin.Instance?.DiagnosticMessage("Inventory opening capture "+capture.Id+" saved; "+
                    capture.Calls(OpeningPhase.PanelIncludingHooks)+" panel / "+capture.Calls(OpeningPhase.ListIncludingHooks)+" list calls; "+
                    "requested features "+(capture.Enabled?"enabled":"disabled")+"; crafting integration "+
                    (capture.CraftingIntegrationObserved?"observed":"absent")+"; "+
                    (capture.ComparisonIssue==null?"comparison valid":"INVALID comparison: "+capture.ComparisonIssue)+
                    ". Timings in diagnostic JSONL; no runtime optimisation applied.");
            }
            catch(Exception error){Disable(error);}
        }
        private void Disable(Exception error)
        {
            _failed=true;_capture=null;
            try{_patches?.UnpatchSelf();}catch(Exception){}
            Plugin.Instance?.DiagnosticMessage("Inventory opening profiling stopped (mod features retained): "+error.GetType().Name+": "+error.Message);
        }
        private void OnDestroy()
        {
            if(_instance==this)_instance=null;
            try{_patches?.UnpatchSelf();}catch(Exception){}
        }
    }
}
