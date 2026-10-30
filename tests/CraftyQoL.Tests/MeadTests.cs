using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using CraftyQoL.Core;

internal static class MeadTests
{
    internal static void Run(Action<string,Action> test)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","vanilla-mead-1.0.20.json")));
        var root=document.RootElement;
        var recipes=root.GetProperty("recipes").EnumerateArray().ToArray();
        var conversions=root.GetProperty("conversions").EnumerateArray().ToArray();
        MeadFlags Effects(JsonElement item)
        {
            if(item.GetProperty("effectClass").GetString()!="SE_Stats")return MeadFlags.Other;
            var fields=item.GetProperty("fields");
            double Field(string f)=>fields.GetProperty(f).ValueKind==JsonValueKind.Number?fields.GetProperty(f).GetDouble():0;
            double Positive(params string[] names)=>MeadEffects.Positive(names.Select(Field).ToArray());
            double Reduction(params string[] names)=>MeadEffects.Reduction(names.Select(Field).ToArray());
            bool resistance=item.GetProperty("mods").EnumerateArray().Any(m=>m.GetProperty("m_type").GetInt32()!=0 && MeadEffects.Resistant(m.GetProperty("m_modifier").GetInt32()));
            bool utility=Reduction("m_jumpStaminaUseModifier","m_attackStaminaUseModifier","m_blockStaminaUseModifier","m_dodgeStaminaUseModifier","m_swimStaminaUseModifier")<0 || Positive("m_addMaxCarryWeight","m_speedModifier","m_swimSpeedModifier","m_addArmor","m_armorMultiplier","m_raiseSkillModifier","m_skillLevelModifier","m_skillLevelModifier2")>0;
            return MeadEffects.Classify(Positive("m_healthUpFront","m_healthOverTime","m_healthPerTick"),Field("m_healthRegenMultiplier"),
                Positive("m_staminaUpFront","m_staminaOverTime"),Field("m_staminaRegenMultiplier"),
                Positive("m_eitrUpFront","m_eitrOverTime"),Field("m_eitrRegenMultiplier"),resistance,utility);
        }
        var graph=new MeadGraph();
        foreach(var c in conversions){graph.Add(c.GetProperty("output").GetString()!,Effects(c));graph.Convert(c.GetProperty("from").GetString()!,c.GetProperty("output").GetString()!);}
        foreach(var r in recipes)graph.Add(r.GetProperty("output").GetString()!,Effects(r));
        test("verified Ketill catalogue and exact station",()=>
        {
            Program.Assert(recipes.Length==21 && conversions.Length==20);
            Program.Assert(root.GetProperty("station").GetProperty("prefab").GetString()=="piece_MeadCauldron");
            Program.Assert(StationRules.IsMead("piece_MeadCauldron") && !StationRules.IsEquipment("piece_MeadCauldron"));
            Program.Assert(!StationRules.IsMead("piece_meadcauldron") && !StationRules.IsActive("piece_MeadCauldron",false));
        });
        var infos=new List<RecipeInfo>();
        foreach(var recipe in recipes)
        {
            string output=recipe.GetProperty("output").GetString()!;
            var flags=graph.Resolve(output);
            test("installed Mead benefit "+output,()=>Program.Assert(flags==(MeadFlags)recipe.GetProperty("expected").GetInt32(),flags.ToString()));
            string key=RecipeInfo.StableKey("piece_MeadCauldron",recipe.GetProperty("recipe").GetString()!,output);
            infos.Add(new RecipeInfo(key,output,output,new FoodStats(0,0,0),NutritionSource.Unknown,
                BiomeCatalogue.ClassifyEquipment(recipe.GetProperty("ingredients").EnumerateArray().Select(i=>i.GetProperty("item").GetString()!).ToArray(),false,null),mead:flags));
            test("Mead nominal one-craft tracker inputs "+output,()=>
            {
                var needs=recipe.GetProperty("ingredients").EnumerateArray().Select(i=>new TrackerNeed(i.GetProperty("item").GetString()!,"ingredient",i.GetProperty("m_amount").GetInt32())).ToArray();
                var plan=new TrackerPlan(key,output,recipe.GetProperty("amount").GetInt32(),needs);
                var materials=TrackerMath.Aggregate(new[]{plan},(id,q)=>0);
                Program.Assert(materials.Sum(m=>m.Required)==needs.Sum(n=>(long)n.Amount));
            });
        }
        test("Mead category and common filters compose independently",()=>
        {
            var favourites=new HashSet<string>(infos.Select(i=>i.Key));
            foreach(var pair in new[]{(MeadCategory.All,21),(MeadCategory.Healing,4),(MeadCategory.Stamina,4),(MeadCategory.Eitr,2),(MeadCategory.Resistance,3),(MeadCategory.Other,8)})
            {
                var query=new QueryState(mead:true){MeadCategory=pair.Item1};
                Program.Assert(MeadQuery.Filter(infos,query,favourites).Count==pair.Item2);
            }
            var q=new QueryState(mead:true){MeadCategory=MeadCategory.Healing,Search="HEALTHMINOR",FavouritesOnly=true};
            Program.Assert(MeadQuery.Filter(infos,q,favourites).Single().Output=="MeadBaseHealthMinor");
            favourites.Clear();Program.Assert(MeadQuery.Filter(infos,q,favourites).Count==0);
            q.FavouritesOnly=false;q.MeadCategory=MeadCategory.Eitr;Program.Assert(MeadQuery.Filter(infos,q,favourites).Count==0);
            q.Search="";q.MeadCategory=MeadCategory.All;
            foreach(BiomeChoice biome in Enum.GetValues<BiomeChoice>())
            {q.Biome=biome;Program.Assert(MeadQuery.Filter(infos,q,favourites).SequenceEqual(infos));}
            q.Biome=BiomeChoice.All;Program.Assert(MeadQuery.Filter(infos,q,favourites).SequenceEqual(infos));
        });
        test("potion category changes invalidate the query in Game and A-Z ordering",()=>
        {
            foreach(EquipmentSort sort in Enum.GetValues<EquipmentSort>())
            foreach(MeadCategory category in Enum.GetValues<MeadCategory>())
            {
                var applied=new QueryState(mead:true){EquipmentSort=sort};
                var next=new QueryState(mead:true){EquipmentSort=sort,MeadCategory=category};
                Program.Assert(next.SameQuery(applied)==(category==MeadCategory.All),"Category refresh incorrectly suppressed");
                var result=MeadQuery.Filter(infos,next,new HashSet<string>());
                Program.Assert(result.Count>0 && result.Count==(category==MeadCategory.All?21:infos.Count(i=>(i.Mead&(MeadFlags)(1<<((int)category-1)))!=0)));
            }
        });
        test("query invalidation tracks query fields and ignores biome disclosure",()=>
        {
            Action<QueryState>[] changes={q=>q.Category=FoodCategory.Health,q=>q.MeadCategory=MeadCategory.Resistance,q=>q.Sort=FoodSort.Stamina,
                q=>q.EquipmentCategory=EquipmentCategory.Weapons,q=>q.EquipmentSort=EquipmentSort.Alphabetical,q=>q.Biome=BiomeChoice.Swamp,
                q=>q.FavouritesOnly=true,q=>q.Search="test"};
            foreach(var change in changes){var q=new QueryState();change(q);Program.Assert(!q.SameQuery(new QueryState()));}
            Program.Assert(new QueryState{BiomesExpanded=true}.SameQuery(new QueryState()));
            Program.Assert(!new QueryState(mead:true).SameQuery(new QueryState()) && !new QueryState(equipment:true).SameQuery(new QueryState()));
        });
        test("Mead effects exclude penalties and invalid values; support overlapping benefits",()=>
        {
            Program.Assert(MeadEffects.Classify(0,0.5,0,2,0,1,false)==MeadFlags.Stamina);
            Program.Assert(MeadEffects.Classify(1,1,1,1,1,1,true,true)==(MeadFlags)31);
            Program.Assert(MeadEffects.Classify(double.NaN,1,0,1,double.PositiveInfinity,double.NaN,false)==MeadFlags.Other);
            Program.Assert(MeadEffects.Positive(double.NaN,double.PositiveInfinity,-10,4)==4 && MeadEffects.Reduction(double.NegativeInfinity,5,-0.3)==-0.3);
        });
        test("resistance metadata excludes weakness, normal and ignore",()=>
        { foreach(int m in new[]{1,3,5,7})Program.Assert(MeadEffects.Resistant(m));foreach(int m in new[]{0,2,4,6,8,999})Program.Assert(!MeadEffects.Resistant(m)); });
        test("Mead conversion ambiguity, cycles and bounded chains fail closed",()=>
        {
            var g=new MeadGraph();g.Add("h",MeadFlags.Healing);g.Add("s",MeadFlags.Stamina);g.Add("h2",MeadFlags.Healing);
            g.Convert("agree","h");g.Convert("agree","h2");Program.Assert(g.Resolve("agree")==MeadFlags.Healing);
            g.Convert("ambiguous","h");g.Convert("ambiguous","s");Program.Assert(g.Resolve("ambiguous")==MeadFlags.Other);
            g.Convert("cycle","cycle");g.Convert("missing","unknown");Program.Assert(g.Resolve("cycle")==MeadFlags.Other && g.Resolve("missing")==MeadFlags.Other);
            g.Add("intermediate",MeadFlags.Eitr);g.Convert("intermediate","h");Program.Assert(g.Resolve("intermediate")==MeadFlags.Healing);
            for(int i=0;i<10;i++)g.Convert("chain"+i,"chain"+(i+1));g.Add("chain10",MeadFlags.Healing);Program.Assert(g.Resolve("chain0")==MeadFlags.Other);
        });
        test("Mead stable pins persist without changing favourite identities",()=>
        {
            string dir=Path.Combine(Path.GetTempPath(),"MeadPin."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string path=Path.Combine(dir,"tracker.json");var store=new TrackerStore(path,_=>{});string key=infos[0].Key;
                store.Toggle(new TrackerPin{Key=key,Station="piece_MeadCauldron",Signature=TrackerMath.Signature("direct")});Program.Assert(store.Save());
                var restored=new TrackerStore(path,_=>{});Program.Assert(restored.State.Pins.Single().Key==key && restored.State.Pins[0].Station=="piece_MeadCauldron");
                Program.Assert(!File.Exists(Path.Combine(dir,"favourites.json")));
            }
            finally{Directory.Delete(dir,true);}
        });
        test("inventory-driven tracker remains independent of Mead filtering",()=>
        {
            Program.Assert(TrackerUiPolicy.Visible(true,true,true,false));
            Program.Assert(!TrackerUiPolicy.Visible(true,true,false,false));
            Program.Assert(TrackerUiPolicy.Visible(true,true,false,true));
            Program.Assert(!TrackerUiPolicy.Visible(false,true,true,true));
            Program.Assert(!TrackerUiPolicy.Visible(true,false,true,true));
            Program.Assert(TrackerUiPolicy.CanEdit(true) && !TrackerUiPolicy.CanEdit(false));
        });
        test("pin width reserves a bounded native Craft slot",()=>
        {
            Program.Assert(TrackerPinGeometry.Size==32 && TrackerPinGeometry.Reserve==40);
            Program.Assert(!TrackerPinGeometry.Fits(float.NaN) && !TrackerPinGeometry.Fits(135) && TrackerPinGeometry.Fits(136));
            foreach(float width in new[]{136f,320f,330f})Program.Assert(width-TrackerPinGeometry.Reserve>=96);
            var geometry=new ToolbarGeometry(330);
            Program.Assert(geometry.AlphabeticalOption(0).Width==geometry.Option(0,true).Width);
        });
    }
}
