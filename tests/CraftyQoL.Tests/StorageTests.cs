using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CraftyQoL.Core;

internal static class StorageTests
{
    private static readonly object Prefab=new object(),Shared=new object();
    private static StorageItem Item(int i,string id="Wood",int count=1,int maximum=50,string metadata="same",bool merge=true,StorageCategory category=StorageCategory.Materials,int? x=null,int? y=null,object? prefab=null,object? shared=null)
        =>new StorageItem(i,id,metadata,category,count,maximum,x??i%8,y??i/8,merge,prefab??Prefab,shared??Shared);
    private static bool Rejected(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
    internal static void Run(Action<string,Action> test)
    {
        test("storage categories follow fixed primary order and hybrid precedence",()=>
        {
            Program.Assert((int)StoragePlanner.Category(1,0,EquipmentFlags.Other,false,false,false)==1);
            Program.Assert((int)StoragePlanner.Category(1,0,EquipmentFlags.Other,true,false,false)==2);
            Program.Assert((int)StoragePlanner.Category(2,0,EquipmentFlags.Other,false,true,false)==3);
            Program.Assert((int)StoragePlanner.Category(2,0,EquipmentFlags.Other,false,true,true)==4);
            Program.Assert(StoragePlanner.Category(3,0,EquipmentFlags.Weapons|EquipmentFlags.Tools,false,false,false)==StorageCategory.Tools);
            Program.Assert(StoragePlanner.Category(3,0,EquipmentFlags.Weapons|EquipmentFlags.Ammunition,false,false,false)==StorageCategory.Ammunition);
            Program.Assert(StoragePlanner.Category(5,0,EquipmentFlags.Shields,false,false,false)==StorageCategory.Armour);
            Program.Assert(StoragePlanner.Category(13,0,EquipmentFlags.Other,false,false,false)==StorageCategory.Valuables);
            Program.Assert(StoragePlanner.Category(1,25,EquipmentFlags.Other,false,false,false)==StorageCategory.Valuables);
            Program.Assert(StoragePlanner.Category(999,0,EquipmentFlags.Other,false,false,false)==StorageCategory.Miscellaneous);
        });
        test("storage partial stacks consolidate to actual maximum before top-left placement",()=>
        {
            var source=new[]{Item(0,count:40,x:6,y:4),Item(1,count:30,x:1,y:3),Item(2,count:15,x:5,y:5)};
            var plan=StoragePlanner.Build(source,8,8);Program.Assert(plan.Changed && plan.Items.Count==2 && plan.Items[0].Count==50 && plan.Items[1].Count==35);
            Program.Assert(plan.Items[0].X==0 && plan.Items[0].Y==0 && plan.Items[1].X==1);
            Program.Assert(source[0].Count==40 && source[1].X==1,"Planner mutated original descriptors");
        });
        test("storage full stacks and max-one items retain every unit",()=>
        {
            var source=new[]{Item(0,count:50),Item(1,count:50),Item(2,"Sword",maximum:1),Item(3,"Sword",maximum:1)};
            var plan=StoragePlanner.Build(source,8,8);Program.Assert(plan.Items.Count==4 && plan.Items.Sum(r=>r.Count)==102);
        });
        test("storage identity is prefab based and ordinal, never localised names",()=>
        {
            var source=new[]{Item(0,"wood",2),Item(1,"Wood",3),Item(2,"Mod.Wood",4)};
            var rows=StoragePlanner.Build(source,8,8).Items;Program.Assert(rows.Count==3 && source[rows[0].Source].Id=="Mod.Wood" && source[rows[1].Source].Id=="Wood");
        });
        test("storage distinct native prefab/shared references never merge",()=>
        {
            var source=new[]{Item(0,count:4),Item(1,count:5,prefab:new object()),Item(2,count:6,shared:new object())};
            Program.Assert(StoragePlanner.Build(source,8,8).Items.Count==3);
        });
        test("storage uncertain custom/modded stacks stay separate even if keys match",()=>
        {
            var source=new[]{Item(0,count:4,merge:false),Item(1,count:5,merge:false),Item(2,count:6)};
            var plan=StoragePlanner.Build(source,8,8);Program.Assert(plan.Items.Count==3 && plan.Items.All(r=>r.Count==source[r.Source].Count));
        });
        string Key(int quality=1,int variant=0,int world=0,float durability=100,bool picked=false,bool equipped=false,bool cheated=false,long crafter=0,string name="",float attack=0,bool projectile=false,IReadOnlyDictionary<string,string>? custom=null)
            =>StorageMetadata.Key("Wood",quality,variant,world,durability,picked,equipped,cheated,crafter,name,attack,projectile,custom??new Dictionary<string,string>());
        test("storage compatibility includes quality variant world durability and all native flags",()=>
        {
            var keys=new[]{Key(),Key(quality:2),Key(variant:1),Key(world:1),Key(durability:99.999f),Key(picked:true),Key(equipped:true),Key(cheated:true),Key(crafter:1),Key(name:"Crafter"),Key(attack:1),Key(projectile:true)};
            Program.Assert(keys.Distinct(StringComparer.Ordinal).Count()==keys.Length);
            var source=keys.Select((key,i)=>Item(i,count:2,metadata:key)).ToArray();Program.Assert(StoragePlanner.Build(source,8,8).Items.Count==keys.Length);
        });
        test("custom metadata identity is deterministic and lossless across key ordering",()=>
        {
            var a=new Dictionary<string,string>{{"b","2"},{"a","1"}};var b=new Dictionary<string,string>{{"a","1"},{"b","2"}};
            Program.Assert(Key(custom:a)==Key(custom:b));b["a"]="different";Program.Assert(Key(custom:a)!=Key(custom:b));
            Program.Assert(Key(custom:a)!=Key());Program.Assert(a["a"]=="1");
            Program.Assert(Rejected(()=>Key(custom:new Dictionary<string,string>{{"x",new string('x',50000)}})));
        });
        test("storage category then identity produces consecutive slots and trailing empties",()=>
        {
            var source=new[]{Item(0,"Z",category:StorageCategory.Miscellaneous,x:7,y:7),Item(1,"Ore",category:StorageCategory.Metals,x:4,y:4),Item(2,"A",x:2,y:3),Item(3,"Food",category:StorageCategory.Food,x:0,y:6)};
            var plan=StoragePlanner.Build(source,8,8);Program.Assert(plan.Items.Select(r=>source[r.Source].Category).SequenceEqual(new[]{StorageCategory.Materials,StorageCategory.Metals,StorageCategory.Food,StorageCategory.Miscellaneous}));
            Program.Assert(plan.Items.Select(r=>r.Y*8+r.X).SequenceEqual(Enumerable.Range(0,4)));
        });
        test("storage repeated sorting is idempotent for full and sparse inventories",()=>
        {
            var random=new Random(1701);
            for(int run=0;run<120;run++)
            {
                int count=random.Next(1,65);var slots=Enumerable.Range(0,64).OrderBy(_=>random.Next()).ToArray();var source=new List<StorageItem>();
                for(int i=0;i<count;i++)source.Add(Item(i,"Item"+random.Next(5),random.Next(1,51),metadata:"q"+random.Next(3),merge:random.Next(4)!=0,category:(StorageCategory)random.Next(1,11),x:slots[i]%8,y:slots[i]/8));
                var plan=StoragePlanner.Build(source,8,8);StoragePlanner.Validate(source,plan,8,8);
                var next=plan.Items.Select((r,i)=>new StorageItem(i,source[r.Source].Id,source[r.Source].Metadata,source[r.Source].Category,r.Count,source[r.Source].Maximum,r.X,r.Y,source[r.Source].Merge,source[r.Source].Prefab,source[r.Source].Shared)).ToArray();
                var again=StoragePlanner.Build(next,8,8);Program.Assert(!again.Changed && again.Items.Sum(r=>r.Count)==source.Sum(r=>r.Count));
            }
        });
        test("storage rejects invalid and overlapping slots without modifying inputs",()=>
        {
            Program.Assert(Rejected(()=>StoragePlanner.Build(new[]{Item(0),Item(1,x:0,y:0)},8,8)));
            foreach(var item in new[]{Item(0,x:-1),Item(0,x:8),Item(0,y:8),Item(0,count:0),Item(0,count:51),Item(0,maximum:0),Item(0,maximum:65536)})
                Program.Assert(Rejected(()=>StoragePlanner.Build(new[]{item},8,8)));
            Program.Assert(Rejected(()=>StoragePlanner.Build(new[]{Item(0)},256,8)));
            Program.Assert(Rejected(()=>StoragePlanner.Build(new[]{Item(0),Item(1)},1,1)));
        });
        test("storage validator detects quantity loss duplication metadata substitution and bad layout",()=>
        {
            var source=new[]{Item(0,count:4,metadata:"q1"),Item(1,count:6,metadata:"q2")};
            foreach(var rows in new[]{new[]{new StoragePlacement(0,10,0,0)},new[]{new StoragePlacement(0,4,0,0),new StoragePlacement(0,6,1,0)},new[]{new StoragePlacement(0,4,1,0),new StoragePlacement(1,6,0,0)},new[]{new StoragePlacement(0,4,0,0)}})
                Program.Assert(Rejected(()=>StoragePlanner.Validate(source,new StoragePlan(rows.ToList(),true),8,8)));
        });
        test("storage empty and already organised plans are no-ops",()=>
        {
            Program.Assert(!StoragePlanner.Build(Array.Empty<StorageItem>(),8,8).Changed);
            Program.Assert(!StoragePlanner.Build(new[]{Item(0,"A",count:50),Item(1,"B",count:4)},8,8).Changed);
        });
        test("storage header gap fits native geometry and declines overlapping or scaled-small layouts",()=>
        {
            var title=new UiBox(201.5f,7.7f,167,23);var stack=new UiBox(435.7f,1.8f,133,40);var take=new UiBox(-1.5f,-3,133,40);var weight=new UiBox(250,290,67,22);
            Program.Assert(StorageGeometry.Place(570,340,title,stack,take,weight,out var button) && button.Width==44 && button.Height==stack.Height && button.Y==stack.Y);
            foreach(float nativeHeight in new[]{32f,40f,48f})
            {
                var native=new UiBox(stack.X,stack.Y,stack.Width,nativeHeight);
                Program.Assert(StorageGeometry.Place(570,340,title,native,take,weight,out var resized) && resized.Height==nativeHeight && resized.Y==native.Y);
            }
            Program.Assert(!StorageGeometry.Place(570,340,title,new UiBox(stack.X,stack.Y,stack.Width,float.NaN),take,weight,out _));
            Program.Assert(!StorageGeometry.Place(570,340,title,new UiBox(stack.X,stack.Y,stack.Width,0),take,weight,out _));
            Program.Assert(!StorageGeometry.Place(400,340,title,stack,take,weight,out _));
            Program.Assert(!StorageGeometry.Place(570,340,title,new UiBox(390,1,133,40),take,weight,out _));
            Program.Assert(!StorageGeometry.Place(570,340,title,stack,take,button,out _));
            Program.Assert(!StorageGeometry.Place(float.NaN,340,title,stack,take,weight,out _));
        });
        test("storage focus protects native movement/actions but leaves inventory toggle available",()=>
        {
            Program.Assert(!InputPolicy.BlockAction("Inventory",FocusMode.Storage));
            foreach(string key in new[]{"JoyDPadUp","JoyDPadDown","JoyButtonB","JoyTabLeft","JoyTabRight"})Program.Assert(InputPolicy.BlockAction(key,FocusMode.Storage));
            Program.Assert(!InputPolicy.BlockAction("UnrelatedAction",FocusMode.Storage));
        });
        test("publication receipt requires completed native save/sync, not merely changed bytes",()=>
        {
            Program.Assert(StorageReceipt.State(true,false,true,false,false,false)==StoragePublication.Unknown);
            Program.Assert(StorageReceipt.State(true,false,false,true,false,false)==StoragePublication.Unknown);
            Program.Assert(StorageReceipt.State(true,false,true,true,false,false)==StoragePublication.Committed);
            Program.Assert(StorageReceipt.State(false,true,false,true,false,false)==StoragePublication.Unchanged);
            Program.Assert(StorageReceipt.State(false,false,true,true,true,false)==StoragePublication.Unknown);
            Program.Assert(StorageReceipt.State(true,true,false,true,false,true)==StoragePublication.Unchanged);
            Program.Assert(StorageReceipt.State(true,true,false,true,true,true)==StoragePublication.Committed);
        });
        test("transaction unchanged or invalid initial state never applies or publishes",()=>
        {
            foreach(bool allowed in new[]{false,true})
            {int apply=0,publish=0;var r=StorageTransaction.Execute(false,()=>allowed,()=>apply++,()=>true,()=>publish++,()=>StoragePublication.Unchanged,()=>true,()=>true,()=>{},()=>true);Program.Assert(apply==0 && publish==0 && r==(allowed?StorageResult.Unchanged:StorageResult.Aborted));}
        });
        test("transaction validates before publishing once",()=>
        {
            int stage=0,notify=0;
            var r=StorageTransaction.Execute(true,()=>true,()=>stage=1,()=>stage==1,()=>{notify++;stage=2;},()=>stage==2?StoragePublication.Committed:StoragePublication.Unchanged,()=>stage>=1,()=>true,()=>stage=0,()=>stage==0);
            Program.Assert(r==StorageResult.Applied && notify==1 && stage==2);
        });
        test("transaction partial apply exceptions restore exact journal without notification",()=>
        {
            int state=0,notify=0;
            var r=StorageTransaction.Execute(true,()=>true,()=>{state=1;throw new Exception();},()=>true,()=>notify++,()=>StoragePublication.Unchanged,()=>false,()=>true,()=>state=0,()=>state==0);
            Program.Assert(r==StorageResult.Aborted && state==0 && notify==0);
        });
        test("transaction access loss or interrupted readiness rolls back locally before publication",()=>
        {
            int state=0,notify=0;
            var r=StorageTransaction.Execute(true,()=>true,()=>state=1,()=>false,()=>notify++,()=>StoragePublication.Unchanged,()=>state==1,()=>true,()=>state=0,()=>state==0);
            Program.Assert(r==StorageResult.Aborted && state==0 && notify==0);
        });
        test("transaction failed native save rolls back unchanged network state",()=>
        {
            int state=0;
            var r=StorageTransaction.Execute(true,()=>true,()=>state=1,()=>true,()=>throw new Exception(),()=>StoragePublication.Unchanged,()=>state==1,()=>true,()=>state=0,()=>state==0);
            Program.Assert(r==StorageResult.Aborted && state==0);
        });
        test("transaction exception after verified publication never rolls back committed data",()=>
        {
            bool saved=false,rolled=false;
            var r=StorageTransaction.Execute(true,()=>true,()=>{},()=>true,()=>{saved=true;throw new Exception();},()=>saved?StoragePublication.Committed:StoragePublication.Unchanged,()=>true,()=>true,()=>rolled=true,()=>true);
            Program.Assert(r==StorageResult.Applied && saved && !rolled);
        });
        test("transaction unknown publication or foreign mutation never overwrites newer state",()=>
        {
            foreach(var published in new[]{StoragePublication.Unknown,StoragePublication.Unchanged})
            {bool rolled=false;var r=StorageTransaction.Execute(true,()=>true,()=>{},()=>false,()=>{},()=>published,()=>false,()=>false,()=>rolled=true,()=>true);Program.Assert(r==StorageResult.Indeterminate && !rolled);}
        });
        test("transaction failed rollback is terminal and never retries publication",()=>
        {
            int notify=0;
            var r=StorageTransaction.Execute(true,()=>true,()=>{},()=>false,()=>notify++,()=>StoragePublication.Unchanged,()=>false,()=>true,()=>throw new Exception(),()=>false);
            Program.Assert(r==StorageResult.Indeterminate && notify==0);
        });
    }
}
