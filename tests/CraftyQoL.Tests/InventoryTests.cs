using System;
using System.Collections.Generic;
using System.Linq;
using CraftyQoL.Core;

internal static class InventoryTests
{
    internal static void Run(Action<string,Action> test)
    {
        test("inventory automatic disclosure uses cost flags, not debug/unlock state",()=>
        {
            foreach(var access in new[]{CraftAccess.None,CraftAccess.AllRecipesUnlocked})
            {var state=new InventoryDisclosure();state.Observe(access);Program.Assert(!state.Expanded);}
            foreach(var access in new[]{CraftAccess.NoCost,CraftAccess.NoCraftCost,CraftAccess.NoCost|CraftAccess.AllRecipesUnlocked})
            {var state=new InventoryDisclosure();state.Observe(access);Program.Assert(state.Expanded);}
        });
        test("manual disclosure wins across mode changes until next inventory session",()=>
        {
            var state=new InventoryDisclosure();state.Observe(CraftAccess.NoCost);state.Toggle();
            foreach(var access in new[]{CraftAccess.None,CraftAccess.NoCraftCost,CraftAccess.NoCost}){state.Observe(access);Program.Assert(!state.Expanded);}
            state.Toggle();state.Observe(CraftAccess.None);Program.Assert(state.Expanded);
            var next=new InventoryDisclosure();next.Observe(CraftAccess.None);Program.Assert(!next.Expanded);
        });
        test("recipe family uses station, effects, nutrition and equipment metadata",()=>
        {
            RecipeFamily F(bool food=false,bool mead=false,bool fermented=false,bool potion=false,FoodStats stats=default,EquipmentFlags eq=EquipmentFlags.Other)=>InventoryRules.Family(food,mead,fermented,potion,stats,eq);
            Program.Assert(F(mead:true,stats:new FoodStats(37,110,0))==RecipeFamily.Mead);
            Program.Assert(F(fermented:true)==RecipeFamily.Mead && F(potion:true)==RecipeFamily.Mead);
            Program.Assert(F(food:true)==RecipeFamily.Food && F(stats:new FoodStats(1,1,0))==RecipeFamily.Food);
            Program.Assert(F(eq:EquipmentFlags.Weapons|EquipmentFlags.Tools)==RecipeFamily.Equipment);
            Program.Assert(InventoryRules.Family(false,false,false,false,default,EquipmentFlags.Other,true)==RecipeFamily.Equipment);
            foreach(int type in new[]{3,4,5,6,7,9,11,12,14,15,17,18,19,22,23,24})Program.Assert(InventoryRules.EquipmentType(type));
            foreach(int type in new[]{0,1,2,8,10,13,16,20,21,999})Program.Assert(!InventoryRules.EquipmentType(type));
            Program.Assert(F()==RecipeFamily.Other && F(stats:new FoodStats(double.NaN,-1,null))==RecipeFamily.Other);
        });
        var biome=new BiomeAssociation(BiomeAssociation.Bit(BiomeChoice.Meadows),BiomeSource.Curated);
        RecipeInfo Info(string id,RecipeFamily family,FoodStats food=default,EquipmentFlags eq=EquipmentFlags.Other,MeadFlags mead=MeadFlags.Other)=>
            new RecipeInfo(id,id,id,food,NutritionSource.Direct,biome,true,eq,mead,family);
        var food=Info("Balanced food",RecipeFamily.Food,new FoodStats(50,50,25));
        var gear=Info("Tool weapon",RecipeFamily.Equipment,eq:EquipmentFlags.Weapons|EquipmentFlags.Tools);
        var potion=Info("Restoration utility",RecipeFamily.Mead,mead:MeadFlags.Healing|MeadFlags.Other);
        var other=Info("Material",RecipeFamily.Other);var source=new[]{food,gear,potion,other};var favourites=new HashSet<string>{food.Key,potion.Key};
        test("inventory All ignores every stale secondary and biome selection",()=>
        {
            var query=new QueryState(inventory:true){Family=RecipeFamily.All,Category=FoodCategory.Eitr,EquipmentCategory=EquipmentCategory.Magic,MeadCategory=MeadCategory.Resistance,Biome=BiomeChoice.DeepNorth};
            Program.Assert(InventoryRules.Filter(source,query,favourites).SequenceEqual(source));
        });
        test("inventory family filters compose categories, search and favourites",()=>
        {
            foreach(var family in new[]{RecipeFamily.Food,RecipeFamily.Equipment,RecipeFamily.Mead,RecipeFamily.Other})
            {var query=new QueryState(inventory:true){Family=family};Program.Assert(InventoryRules.Filter(source,query,favourites).Single().Family==family);}
            var q=new QueryState(inventory:true){Family=RecipeFamily.Food,Category=FoodCategory.Stamina,Search="BALANCED",FavouritesOnly=true};
            Program.Assert(InventoryRules.Filter(source,q,favourites).Single()==food);
            q.Category=FoodCategory.Eitr;Program.Assert(InventoryRules.Filter(source,q,favourites).Single()==food);
            q.Search="missing";Program.Assert(InventoryRules.Filter(source,q,favourites).Count==0);
        });
        test("inventory equipment and Mead category overlap reuses existing flags",()=>
        {
            var q=new QueryState(inventory:true){Family=RecipeFamily.Equipment};
            foreach(var c in new[]{EquipmentCategory.Weapons,EquipmentCategory.Tools}){q.EquipmentCategory=c;Program.Assert(InventoryRules.Filter(source,q,favourites).Single()==gear);}
            q.Family=RecipeFamily.Mead;q.Biome=BiomeChoice.DeepNorth;
            foreach(var c in new[]{MeadCategory.Healing,MeadCategory.Other}){q.MeadCategory=c;Program.Assert(InventoryRules.Filter(source,q,favourites).Single()==potion);}
            q.MeadCategory=MeadCategory.Eitr;Program.Assert(InventoryRules.Filter(source,q,favourites).Count==0);
        });
        test("inventory biomes apply only to Food and Equipment",()=>
        {
            foreach(var family in new[]{RecipeFamily.All,RecipeFamily.Other,RecipeFamily.Mead})
            {var q=new QueryState(inventory:true){Family=family,Biome=BiomeChoice.DeepNorth};Program.Assert(InventoryRules.Filter(source,q,favourites).Count>0);Program.Assert(!InventoryRules.HasBiomes(family));}
            foreach(var family in new[]{RecipeFamily.Food,RecipeFamily.Equipment})
            {var q=new QueryState(inventory:true){Family=family,Biome=BiomeChoice.DeepNorth};Program.Assert(InventoryRules.Filter(source,q,favourites).Count==0);q.Biome=BiomeChoice.Meadows;Program.Assert(InventoryRules.Filter(source,q,favourites).Count==1);}
        });
        test("inventory query equality includes family/mode and excludes disclosure",()=>
        {
            var a=new QueryState(inventory:true);var b=new QueryState(inventory:true);Program.Assert(a.SameQuery(b));
            b.Disclosure.Toggle();b.BiomesExpanded=true;Program.Assert(a.SameQuery(b));b.Family=RecipeFamily.Food;Program.Assert(!a.SameQuery(b));
            Program.Assert(!a.SameQuery(new QueryState()));
        });
        test("inventory filtering preserves source and canonical cross-view IDs",()=>
        {
            var before=source.ToArray();InventoryRules.Filter(source,new QueryState(inventory:true){Family=RecipeFamily.Mead},favourites);Program.Assert(source.SequenceEqual(before));
            Program.Assert(RecipeInfo.StableKey("piece_cauldron","Recipe_Food","Food")=="v1/piece_cauldron/Recipe_Food/Food");
            Program.Assert(RecipeInfo.StableKey("","Recipe_Hammer","Hammer")=="v1//Recipe_Hammer/Hammer");
            Program.Assert(InventoryRules.Filter(Array.Empty<RecipeInfo>(),new QueryState(inventory:true),favourites).Count==0);
        });
        test("inventory geometry fits every family/disclosure/biome combination without overlap",()=>
        {
            for(float width=170;width<=600;width+=7)
            foreach(RecipeFamily family in Enum.GetValues(typeof(RecipeFamily)))
            foreach(bool expanded in new[]{false,true})foreach(bool biomes in new[]{false,true})
            {
                var g=new InventoryToolbarGeometry(width,family,expanded,biomes);var boxes=new List<UiBox>{g.Toggle};
                if(expanded)
                {
                    boxes.Add(g.Sort(0));boxes.Add(g.Sort(1));boxes.Add(g.Search);boxes.Add(g.Clear);boxes.Add(g.Favourites);
                    for(int i=0;i<5;i++)boxes.Add(g.Family(i));
                    int count=family==RecipeFamily.Equipment?8:family==RecipeFamily.Mead?6:family==RecipeFamily.Food?4:0;
                    for(int i=0;i<count;i++)boxes.Add(g.Category(i,family));
                    if(InventoryRules.HasBiomes(family)){boxes.Add(g.Disclosure);boxes.Add(g.Reset);if(biomes)for(int i=0;i<10;i++)boxes.Add(g.Biome(i));}
                }
                Program.Assert(g.Height<=205 && g.Height>=30 && 320-g.Height>=115);
                foreach(var box in boxes)Program.Assert(box.X>=1.99 && box.Right<=width-1.99 && box.Y>=1.99 && box.Bottom<=g.SeparatorY+.01 && box.Width>0);
                for(int i=0;i<boxes.Count;i++)for(int j=i+1;j<boxes.Count;j++)
                {var a=boxes[i];var b=boxes[j];Program.Assert(!(a.X<b.Right-.01 && b.X<a.Right-.01 && a.Y<b.Bottom-.01 && b.Y<a.Bottom-.01),"Overlapping inventory controls");}
                Program.Assert(Math.Abs(g.Sort(0).Width-g.Biome(0).Width)<.001);
            }
        });
        test("large mixed inventory query preserves vanilla source and deterministic A-Z ties",()=>
        {
            var large=new List<RecipeInfo>();var marked=new HashSet<string>();
            for(int i=0;i<5000;i++)
            {
                var family=(RecipeFamily)(1+i%4);string key=RecipeInfo.StableKey(i%2==0?"piece_cauldron":"","Recipe_"+i,"Item_"+i);
                large.Add(new RecipeInfo(key,family+" "+(i%17),"Item_"+i,new FoodStats(50,50,0),NutritionSource.Direct,biome,true,EquipmentFlags.Weapons|EquipmentFlags.Tools,MeadFlags.Other,family));
                if(i%3==0)marked.Add(key);
            }
            var original=large.ToArray();var q=new QueryState(inventory:true){Family=RecipeFamily.Food,Category=FoodCategory.Stamina,Search="FOOD",FavouritesOnly=true};
            var result=InventoryRules.Filter(large,q,marked);
            Program.Assert(result.Count>400 && result.All(r=>r.Family==RecipeFamily.Food && marked.Contains(r.Key)) && large.SequenceEqual(original));
            var gameOrder=result.ToArray();Program.Assert(result.SequenceEqual(original.Where(r=>r.Family==RecipeFamily.Food && marked.Contains(r.Key))));
            StableOrder.Sort(result,EquipmentQuery.Compare);var first=result.Select(r=>r.Key).ToArray();
            result=gameOrder.Reverse().ToList();StableOrder.Sort(result,EquipmentQuery.Compare);Program.Assert(result.Select(r=>r.Key).SequenceEqual(first));
        });
        test("creative pin disclosure requires inventory origin, native availability and unlock grant",()=>
        {
            foreach(bool origin in new[]{false,true})foreach(bool available in new[]{false,true})
            foreach(var access in new[]{CraftAccess.None,CraftAccess.NoCraftCost,CraftAccess.NoCost,CraftAccess.AllRecipesUnlocked})
            {
                bool revealed=InventoryRules.CreativePin(origin,access,available);
                Program.Assert(revealed==(origin && available && (access==CraftAccess.NoCost || access==CraftAccess.AllRecipesUnlocked)));
            }
            Program.Assert(!InventoryRules.CreativePin(true,CraftAccess.None,true),"Creative exit must fail closed even if old availability was cached");
        });
        test("creative knowledge grants and free-cost disclosure have separate meanings",()=>
        {
            Program.Assert(InventoryRules.CreativeKnowledge(CraftAccess.NoCost));Program.Assert(InventoryRules.CreativeKnowledge(CraftAccess.AllRecipesUnlocked));
            Program.Assert(!InventoryRules.CreativeKnowledge(CraftAccess.None) && !InventoryRules.CreativeKnowledge(CraftAccess.NoCraftCost));
        });
    }
}
