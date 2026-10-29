using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CraftyQoL.Core;

internal static class TrackerTests
{
    internal static void Run(Action<string, Action> test)
    {
        TrackerPlan Plan(params TrackerNeed[] needs) => new TrackerPlan("id", "name", 20, needs);
        test("tracker aggregates direct inputs once per craft, not per output unit", () =>
        {
            var rows = TrackerMath.Aggregate(new[] { Plan(new TrackerNeed("wood", "Wood", 8), new TrackerNeed("resin", "Resin", 2)), Plan(new TrackerNeed("wood", "Wood", 4)) }, (id,q) => id == "wood" ? 9 : 3);
            Program.Assert(rows[0].Required == 12 && rows[0].Owned == 9 && rows[0].Deficit == 3 && !rows[0].Complete);
            Program.Assert(rows[1].Required == 2 && rows[1].Complete);
        });
        test("tracker specific qualities cannot double-count general demand", () =>
        {
            var plans = new[] { Plan(new TrackerNeed("fish", "Fish", 10, 2), new TrackerNeed("fish", "Fish", 20)) };
            var m = TrackerMath.Aggregate(plans, (id,q) => q < 0 ? 20 : 10).Single();
            Program.Assert(m.Required == 30 && m.Owned == 20 && m.Deficit == 10);
            m = TrackerMath.Aggregate(plans, (id,q) => q < 0 ? 30 : 0).Single(); Program.Assert(m.Deficit == 10);
        });
        test("tracker rejects malformed resource quantities and qualities",()=>
        {
            foreach(var need in new[]{new TrackerNeed("a","A",0),new TrackerNeed("a","A",1000001),new TrackerNeed("a","A",1,0),new TrackerNeed("a","A",1,1001)})
            {
                bool rejected=false;try{TrackerMath.Aggregate(new[]{Plan(need)},(id,q)=>0);}catch(InvalidDataException){rejected=true;}
                Program.Assert(rejected);
            }
        });
        test("tracker empty/unavailable plans contribute nothing", () => Program.Assert(TrackerMath.Aggregate(Array.Empty<TrackerPlan>(), (id,q) => throw new Exception()).Count == 0));
        test("tracker separate stable IDs do not merge translated names", () =>
        {
            var rows = TrackerMath.Aggregate(new[] { Plan(new TrackerNeed("a", "Same", 2), new TrackerNeed("b", "Same", 3)) }, (id,q) => 0);
            Program.Assert(rows.Count == 2 && rows[1].Required == 3);
        });
        test("tracker base and alternative output math", () =>
        {
            Program.Assert(TrackerMath.Output(20,1,1,0) == 20 && TrackerMath.Output(1,3,0.5,2) == 4);
            Program.Assert(TrackerMath.Output(1,2,0.6,0) == 2);
        });
        test("tracker scopes are independent and never use names or paths", () =>
        {
            var a = TrackerMath.Scope(12,34);
            Program.Assert(a != TrackerMath.Scope(12,35) && a != TrackerMath.Scope(13,34) && !a.Contains("/") && !a.Contains("\\"));
            Program.Assert(TrackerMath.Clamp(float.NaN) == 0 && TrackerMath.Clamp(2) == 1 && TrackerMath.Clamp(-1) == 0);
        });
        test("tracker layout clamps against canvas scale and retains normalised position",()=>
        {
            foreach(var size in new[]{(240f,180f),(1280f,720f),(1920f,1080f),(3840f,2160f)})
            {
                var g=new TrackerLayout(size.Item1,size.Item2);Program.Assert(g.CanFit);
                foreach(float n in new[]{0f,0.5f,1f})
                { Program.Assert(g.Left(n)>=8 && g.Left(n)+g.Width<=size.Item1-8);Program.Assert(g.Top(n)>=8 && g.Top(n)+g.Height<=size.Item2-8); }
                if(g.RangeX>0)Program.Assert(Math.Abs(g.NormalX(g.Left(0.5f))-0.5f)<0.001f);
                Program.Assert(g.NormalX(-100)==0 && g.NormalY(100000)==1);
            }
            Program.Assert(!new TrackerLayout(120,120).CanFit && !new TrackerLayout(float.NaN,720).CanFit);
        });
        test("fresh tracker position is top-centred across screen sizes and panel heights",()=>
        {
            var state=new TrackerDocument();
            Program.Assert(state.X==0.5f && state.Y==0f && !state.KeepVisible);
            foreach(var size in new[]{(width:1920f,height:1080f),(width:2560f,height:1440f),(width:3440f,height:1440f),(width:800f,height:600f)})
                foreach(float height in new[]{26f,130f,300f})
                {
                    var layout=new TrackerLayout(size.width,size.height,height);
                    Program.Assert(Math.Abs(layout.Left(state.X)+layout.Width/2-size.width/2)<0.001f);
                    Program.Assert(layout.Top(state.Y)==8f);
                }
        });
        test("inventory default, pinned gameplay and safety guards compose independently",()=>
        {
            foreach(bool allowed in new[]{false,true})
            foreach(bool pins in new[]{false,true})
            foreach(bool inventory in new[]{false,true})
            foreach(bool keep in new[]{false,true})
            {
                bool actual=TrackerUiPolicy.Visible(allowed,pins,inventory,keep);
                if(!allowed || !pins)Program.Assert(!actual,"Pin must not bypass safety/empty guards");
                else Program.Assert(actual==(inventory || keep),"Default must follow inventory, pin keeps passive gameplay view");
                Program.Assert(TrackerUiPolicy.CanEdit(inventory)==inventory);
            }
        });
        test("tracker focus consumes recipe navigation but preserves native inventory toggle",()=>
        {
            Program.Assert(!InputPolicy.BlockAction("Inventory",FocusMode.Tracker));
            Program.Assert(!InputPolicy.BlockAction("JoyButtonY",FocusMode.Tracker));
            foreach(string action in new[]{"JoyTabLeft","JoyTabRight","JoyDPadUp","JoyDPadDown","JoyButtonB"})
                Program.Assert(InputPolicy.BlockAction(action,FocusMode.Tracker));
            Program.Assert(!InputPolicy.BlockAction("JoyTabRight",FocusMode.Toolbar));
            Program.Assert(!InputPolicy.BlockAction("UnrelatedAction",FocusMode.Tracker));
        });
        test("compact tracker quantities retain exact carried/required counts",()=>
        {
            Program.Assert(TrackerText.Quantity(4,10)=="4/10");
            Program.Assert(TrackerText.Quantity(12,10)=="12/10");
            Program.Assert(TrackerText.Quantity(0,10)=="0/10");
            Program.Assert(TrackerText.Quantity(64000000,64000000)=="64000000/64000000");
            var totals=TrackerMath.Aggregate(new[]{Plan(new TrackerNeed("fish","Fish",10,2),new TrackerNeed("fish","Fish",20))},(id,q)=>q<0?30:0).Single();
            Program.Assert(TrackerText.Quantity(totals.Owned,totals.Required)=="30/30" && !totals.Complete && totals.Deficit==10);
        });
        test("compact tracker fits content and bounds long lists",()=>
        {
            foreach(var size in new[]{(240f,180f),(1280f,720f),(1920f,1080f),(3840f,2160f)})
            foreach(bool edit in new[]{false,true})foreach(bool settings in new[]{false,true})foreach(bool warning in new[]{false,true})
            foreach(int rows in new[]{1,2,8,12,128,8192})
            {
                var g=new TrackerHudGeometry(size.Item1,size.Item2,rows,edit,settings,false,warning);
                Program.Assert(g.Frame.CanFit && g.Frame.Width<=280 && g.Frame.Height<=300 && g.Frame.Height<=size.Item2-16);
                Program.Assert(g.ViewportHeight>=TrackerHudGeometry.RowHeight);
                Program.Assert(g.ViewportY+g.ViewportHeight<=g.Frame.Height-6);
                Program.Assert(Math.Ceiling(g.ViewportHeight/TrackerHudGeometry.RowHeight)+1<=12,"Virtual row pool insufficient");
                if(!g.Scrolling)Program.Assert(g.ViewportHeight==rows*TrackerHudGeometry.RowHeight,"Unnecessary blank row space");
                for(float y=0;y<=1;y+=0.5f)Program.Assert(g.Frame.Top(y)+g.Frame.Height<=size.Item2-8);
            }
        });
        test("tracker settings and minimisation reserve only needed space",()=>
        {
            var normal=new TrackerHudGeometry(1920,1080,1,false,false,false,false);
            var edit=new TrackerHudGeometry(1920,1080,1,true,false,false,false);
            var recipes=new TrackerHudGeometry(1920,1080,1,true,true,false,false);
            Program.Assert(normal.Frame.Height==82 && edit.Frame.Height==82 && recipes.Frame.Height==106);
            Program.Assert(!normal.Scrolling && !edit.Scrolling);
            var large=new TrackerHudGeometry(1920,1080,128,true,false,false,false);Program.Assert(large.Scrolling && large.Frame.Height==300);
            foreach(bool editing in new[]{false,true})
            {var mini=new TrackerHudGeometry(1920,1080,128,editing,true,true,true);Program.Assert(mini.Frame.Height==26 && mini.ViewportHeight==0 && !mini.Scrolling);}
        });
        void FileCase(string name, Action<string> run) => test(name, () =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "FoodTracker.Tests." + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try { run(Path.Combine(dir,"tracker.json")); }
            finally { foreach (string file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
        });
        TrackerPin Pin(string key) => new TrackerPin { Key = key, Signature = TrackerMath.Signature(key) };
        FileCase("tracker pins/order/preferences/choices survive restart separately from favourites", path =>
        {
            var s = new TrackerStore(path,_=>{}); s.Toggle(Pin("b")); s.Toggle(Pin("a"));
            s.State.X=0.7f; s.State.Y=0.8f; s.State.HideCompleted=true; s.State.Minimised=true; s.State.RecipesTab=true; s.State.KeepVisible=true;
            s.State.Pins[0].Alternative="Fish2";s.State.Pins[0].Quality=3;s.State.Pins[0].Expanded=true;Program.Assert(s.Save());
            var load = new TrackerStore(path,_=>{});Program.Assert(load.State.Pins.Select(p=>p.Key).SequenceEqual(new[]{"b","a"}));
            Program.Assert(load.State.X==0.7f && load.State.HideCompleted && load.State.Minimised && load.State.RecipesTab && load.State.KeepVisible && load.State.Pins[0].Alternative=="Fish2" && load.State.Pins[0].Quality==3);
            load.Toggle(Pin("a"));Program.Assert(load.Save() && new TrackerStore(path,_=>{}).State.Pins.Count==1);
            Program.Assert(!File.Exists(Path.Combine(Path.GetDirectoryName(path)!,"favourites.json")));
        });
        FileCase("old tracker preferences default to inventory-only without losing pins",path=>
        {
            File.WriteAllText(path,"{\"version\":1,\"pins\":[{\"key\":\"existing\",\"signature\":\""+TrackerMath.Signature("existing")+"\",\"alternative\":\"\",\"quality\":1}],\"x\":0.7,\"y\":0.8,\"recipesTab\":true}");
            var store=new TrackerStore(path,_=>{});
            Program.Assert(!store.State.KeepVisible && store.Contains("existing") && store.State.RecipesTab && Math.Abs(store.State.X-0.7)<0.001);
            store.State.KeepVisible=true;store.Touch();Program.Assert(store.Save());
            var restored=new TrackerStore(path,_=>{});Program.Assert(restored.State.KeepVisible && restored.Contains("existing"));
            Program.Assert(File.Exists(path+".bak"));
            restored.State.KeepVisible=false;restored.Touch();Program.Assert(restored.Save());
            Program.Assert(!new TrackerStore(path,_=>{}).State.KeepVisible);
        });
        FileCase("inventory and station views share one canonical pin without changing favourites",path=>
        {
            string key=RecipeInfo.StableKey("forge","Recipe_SwordIron","SwordIron");
            var station=new TrackerPin{Key=key,Signature=TrackerMath.Signature(key),Station="forge"};
            var inventory=new TrackerPin{Key=key,Signature=station.Signature,Station=InventoryRules.Origin};
            var store=new TrackerStore(path,_=>{});store.Toggle(station);store.Toggle(inventory);Program.Assert(store.State.Pins.Count==0);
            store.Toggle(inventory);Program.Assert(store.Save());
            var load=new TrackerStore(path,_=>{});Program.Assert(load.State.Pins.Single().Key==key && load.State.Pins[0].Station==InventoryRules.Origin);
            load.Toggle(station);Program.Assert(load.State.Pins.Count==0);
            string stationless=RecipeInfo.StableKey("","Recipe_Hammer","Hammer");load.Toggle(new TrackerPin{Key=stationless,Signature=TrackerMath.Signature(stationless),Station=InventoryRules.Origin});
            Program.Assert(load.Save() && new TrackerStore(path,_=>{}).Contains(stationless));
            Program.Assert(!File.Exists(Path.Combine(Path.GetDirectoryName(path)!,"favourites.json")));
        });
        FileCase("Galdr pins retain stable scoped identities and nominal one-craft inputs",path=>
        {
            string key=RecipeInfo.StableKey("piece_magetable","Recipe_StaffFireball","StaffFireball");
            var pin=new TrackerPin{Key=key,Signature=TrackerMath.Signature("galdr-direct-inputs"),Station="piece_magetable"};
            var store=new TrackerStore(path,_=>{});Program.Assert(store.Toggle(pin) && store.Save());
            var restored=new TrackerStore(path,_=>{});Program.Assert(restored.State.Pins.Single().Key==key && restored.State.Pins.Single().Station=="piece_magetable");
            var plan=new TrackerPlan(key,"Staff",1,new[]{new TrackerNeed("YggdrasilWood","Wood",20),new TrackerNeed("Eitr","Eitr",16),new TrackerNeed("SurtlingCore","Core",4)});
            var totals=TrackerMath.Aggregate(new[]{plan},(id,q)=>id=="Eitr"?10:20);
            Program.Assert(totals.Single(m=>m.Id=="Eitr").Required==16 && totals.Single(m=>m.Id=="Eitr").Deficit==6);
        });
        FileCase("tracker duplicate toggle/unavailable keys remain unique and preserved",path=>
        {
            var s = new TrackerStore(path,_=>{});s.Toggle(Pin("missingMod"));Program.Assert(s.Save());
            var t = new TrackerStore(path,_=>{});Program.Assert(t.Contains("missingMod"));t.Toggle(Pin("missingMod"));Program.Assert(t.State.Pins.Count==0);
        });
        FileCase("tracker newer schema/corruption never overwritten",path=>
        {
            File.WriteAllText(path,"{\"version\":9,\"pins\":[]}");var s=new TrackerStore(path,_=>{});s.Toggle(Pin("a"));Program.Assert(!s.Save() && File.ReadAllText(path).Contains(":9"));
            File.WriteAllText(path,"broken");s=new TrackerStore(path,_=>{});s.Toggle(Pin("a"));Program.Assert(!s.Save() && File.ReadAllText(path)=="broken");
        });
        FileCase("tracker atomic backup recovery",path=>
        {
            var s = new TrackerStore(path,_=>{});s.Toggle(Pin("a"));s.Save();s.Toggle(Pin("b"));s.Save();File.WriteAllText(path,"broken");
            var t = new TrackerStore(path,_=>{});Program.Assert(t.Contains("a") && !t.Contains("b"));t.Toggle(Pin("c"));Program.Assert(t.Save());
            Program.Assert(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.corrupt.*").Length==1);
        });
        FileCase("tracker max pins and write denial stay bounded/in memory",path=>
        {
            var s = new TrackerStore(path,_=>{});for(int i=0;i<64;i++)Program.Assert(s.Toggle(Pin(i.ToString())));
            Program.Assert(!s.Toggle(Pin("extra")) && s.State.Pins.Count==64);s.Remove("0");Program.Assert(s.Toggle(Pin("extra")));
            File.WriteAllText(path,"block");var blocked=new TrackerStore(Path.Combine(path,"child.json"),_=>{});blocked.Toggle(Pin("a"));Program.Assert(!blocked.Save() && blocked.Contains("a") && !blocked.Dirty);
        });
    }
}
