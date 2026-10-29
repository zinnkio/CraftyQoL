using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using CraftyQoL.Core;

internal static class Compatibility121Tests
{
    internal static void Run(Action<string,Action> test)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","food-station-moves-1.0.21.json")));
        var root=document.RootElement;var rows=root.GetProperty("moves").EnumerateArray().ToArray();
        string Signature(JsonElement r)
        {
            string key=RecipeInfo.StableKey(r.GetProperty("station").GetString()!,r.GetProperty("recipe").GetString()!,r.GetProperty("output").GetString()!);
            var text=new StringBuilder(key);text.Append('|').Append(r.GetProperty("amount").GetInt32()).Append('|').Append(r.GetProperty("onlyOneIngredient").GetInt32()!=0)
                .Append('|').Append(r.GetProperty("qualityResultAmountMultiplier").GetDouble().ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append('|').Append(r.GetProperty("minLevel").GetInt32());
            foreach(var q in r.GetProperty("ingredients").EnumerateArray())text.Append('|').Append(Uri.EscapeDataString(q.GetProperty("item").GetString()!)).Append(':').Append(q.GetProperty("m_amount").GetInt32())
                .Append(':').Append(q.GetProperty("m_upgraderResource").GetInt32()!=0).Append(':').Append(q.GetProperty("m_extraAmountOnlyOneIngredient").GetInt32());
            return TrackerMath.Signature(text.ToString());
        }
        TrackerPin Old(RecipeMove m)=>new TrackerPin{Key=m.OldKey,Signature=m.OldSignature,Station=RecipeMove.OldStation,Expanded=true,Quality=2};
        TrackerPin Current(RecipeMove m)=>new TrackerPin{Key=m.NewKey,Signature=m.NewSignature,Station=RecipeMove.NewStation};
        foreach(var row in rows)
        {
            var after=row.GetProperty("after");string name=after.GetProperty("recipe").GetString()!;
            var move=RecipeMoves.All.Single(m=>m.Recipe==name);
            test("verified1.0.21 identity and structure "+name,()=>
            {
                var before=row.GetProperty("before");
                Program.Assert(before.GetProperty("station").GetString()==RecipeMove.OldStation && after.GetProperty("station").GetString()==RecipeMove.NewStation);
                Program.Assert(after.GetProperty("minLevel").GetInt32()==1 && before.GetProperty("minLevel").GetInt32()>1);
                foreach(var property in after.EnumerateObject())
                    if(property.Name!="station" && property.Name!="minLevel")Program.Assert(property.Value.GetRawText()==before.GetProperty(property.Name).GetRawText(),property.Name);
                Program.Assert(Signature(before)==move.OldSignature && Signature(after)==move.NewSignature);
                Program.Assert(move.OldKey==row.GetProperty("oldKey").GetString() && move.NewKey==row.GetProperty("newKey").GetString());
                var biome=BiomeCatalogue.Classify(move.Output,after.GetProperty("ingredients").EnumerateArray().Select(q=>q.GetProperty("item").GetString()!).ToArray(),false,null);
                Program.Assert(biome.Mask==row.GetProperty("expectedBiomeMask").GetInt32() && biome.Source==BiomeSource.Estimated);
            });
            test("migration approval fails closed for changed or ambiguous metadata "+name,()=>
            {
                Program.Assert(move.Approves(move.NewKey,move.NewSignature,true,false,true));
                Program.Assert(!move.Approves(move.OldKey,move.OldSignature,true,false,true));
                Program.Assert(!move.Approves(move.NewKey,"modded",true,false,true));
                Program.Assert(!move.Approves(move.NewKey,move.NewSignature,false,false,true));
                Program.Assert(!move.Approves(move.NewKey,move.NewSignature,true,true,true));
                Program.Assert(!move.Approves(move.NewKey,move.NewSignature,true,false,false));
                var old=Old(move);Program.Assert(move.MatchesOld(old));old.Signature=TrackerMath.Signature("changed");Program.Assert(!move.MatchesOld(old));
            });
        }
        test("unchanged oven conversions resolve finished nutrition, never ingredient placeholders",()=>
        {
            var graph=new FoodGraph();
            foreach(var food in root.GetProperty("foods").EnumerateArray())
                if(food.GetProperty("itemType").GetInt32()==2)graph.AddEdible(food.GetProperty("prefab").GetString()!,new FoodStats(food.GetProperty("health").GetDouble(),food.GetProperty("stamina").GetDouble(),food.GetProperty("eitr").GetDouble()));
            foreach(var conversion in root.GetProperty("ovenConversions").EnumerateArray())graph.AddFinish(conversion.GetProperty("from").GetString()!,conversion.GetProperty("to").GetString()!);
            foreach(var move in RecipeMoves.All)
            {var food=graph.Resolve(move.Output);Program.Assert(food.Source==NutritionSource.Cooked && food.Stats.Categories==(move.Recipe=="Recipe_KaleChips"?FoodFlags.Stamina:FoodFlags.Eitr));}
        });
        void FileCase(string name,Action<string> body)=>test(name,()=>
        {
            string directory=Path.Combine(Path.GetTempPath(),"RecipeMove121."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try{body(directory);}finally{Directory.Delete(directory,true);}
        });
        FileCase("three favourite IDs migrate atomically, keep unrelated IDs and original backup",dir=>
        {
            string path=Path.Combine(dir,"favourites.json");var old=new FavouriteStore(path,_=>{});
            foreach(var move in RecipeMoves.All)old.Toggle(move.OldKey);old.Toggle("v1/mod/unknown/output");Program.Assert(old.Save());
            var update=new FavouriteStore(path,_=>{});Program.Assert(!update.Migrate(Array.Empty<RecipeMove>()));
            Program.Assert(update.Migrate(RecipeMoves.All) && update.Save());
            var restored=new FavouriteStore(path,_=>{});Program.Assert(RecipeMoves.All.All(m=>restored.Keys.Contains(m.NewKey) && !restored.Keys.Contains(m.OldKey)) && restored.Keys.Contains("v1/mod/unknown/output"));
            Program.Assert(PreferenceFiles.Read<FavouriteDocument>(path+".bak").Recipes.Contains(RecipeMoves.All[0].OldKey));
            Program.Assert(!restored.Migrate(RecipeMoves.All) && !restored.Dirty);
            restored.Toggle(RecipeMoves.All[0].NewKey);Program.Assert(restored.Save() && !new FavouriteStore(path,_=>{}).Keys.Contains(RecipeMoves.All[0].NewKey));
        });
        FileCase("tracker migration preserves scoped position/preferences/order/expansion/quality and backup",dir=>
        {
            string path=Path.Combine(dir,"tracker.json");var old=new TrackerStore(path,_=>{});
            foreach(var move in RecipeMoves.All)old.Toggle(Old(move));old.Toggle(new TrackerPin{Key="modded",Signature=TrackerMath.Signature("custom"),Station="forge"});
            old.State.X=.7f;old.State.Y=.4f;old.State.Minimised=true;old.State.HideCompleted=true;old.State.RecipesTab=true;old.State.KeepVisible=true;old.Save();
            var update=new TrackerStore(path,_=>{});Program.Assert(update.Migrate(RecipeMoves.All) && update.Save());
            var restored=new TrackerStore(path,_=>{});Program.Assert(restored.State.Pins.Select(p=>p.Key).SequenceEqual(RecipeMoves.All.Select(m=>m.NewKey).Concat(new[]{"modded"})));
            Program.Assert(restored.State.X==.7f && restored.State.Y==.4f && restored.State.Minimised && restored.State.HideCompleted && restored.State.RecipesTab && restored.State.KeepVisible);
            for(int i=0;i<3;i++){var pin=restored.State.Pins[i];Program.Assert(pin.Signature==RecipeMoves.All[i].NewSignature && pin.Station==RecipeMove.NewStation && pin.Expanded && pin.Quality==2);}
            Program.Assert(PreferenceFiles.Read<TrackerDocument>(path+".bak").Pins[0].Key==RecipeMoves.All[0].OldKey);
            Program.Assert(!restored.Migrate(RecipeMoves.All) && !restored.Dirty);
        });
        test("duplicate migrated pins merge deterministically; unidentified conflicts and changed pins preserved",()=>
        {
            var m=RecipeMoves.All[0];
            foreach(bool oldFirst in new[]{false,true})
            {
                var pins=oldFirst?new List<TrackerPin>{Old(m),Current(m)}:new List<TrackerPin>{Current(m),Old(m)};
                Program.Assert(RecipeMoves.MigratePins(pins,new[]{m}) && pins.Count==1 && pins[0].Key==m.NewKey && pins[0].Expanded);
            }
            var wrong=Current(m);wrong.Signature=TrackerMath.Signature("unidentified");var conflict=new List<TrackerPin>{Old(m),wrong};
            Program.Assert(!RecipeMoves.MigratePins(conflict,new[]{m}) && conflict.Count==2 && conflict[0].Key==m.OldKey);
            var changed=Old(m);changed.Signature=TrackerMath.Signature("changed");Program.Assert(!RecipeMoves.MigratePins(new List<TrackerPin>{changed},new[]{m}) && changed.Key==m.OldKey);
            var origin=Old(m);origin.Station="modded";Program.Assert(!RecipeMoves.MigratePins(new List<TrackerPin>{origin},new[]{m}));
        });
        FileCase("unsupported/corrupt preference files are never overwritten by migration",dir=>
        {
            foreach(string content in new[]{"broken","{\"version\":9,\"recipes\":[],\"pins\":[]}"})
            {
                string path=Path.Combine(dir,"data.json");File.WriteAllText(path,content);
                var favourites=new FavouriteStore(path,_=>{});Program.Assert(!favourites.Migrate(RecipeMoves.All) && File.ReadAllText(path)==content);
                var tracker=new TrackerStore(path,_=>{});Program.Assert(!tracker.Migrate(RecipeMoves.All) && File.ReadAllText(path)==content);
            }
        });
        test("old biome overrides alias in memory; explicit new override wins without removing old keys",()=>
        {
            var m=RecipeMoves.All[0];var association=new BiomeAssociation(256,BiomeSource.Override);
            var overrides=new Dictionary<string,BiomeAssociation>{{m.OldKey,association}};
            RecipeMoves.AliasOverrides(overrides,new[]{m});Program.Assert(overrides.Count==2 && overrides[m.NewKey].Mask==256);
            overrides[m.NewKey]=new BiomeAssociation(0,BiomeSource.Override);RecipeMoves.AliasOverrides(overrides,new[]{m});Program.Assert(overrides[m.NewKey].Mask==0 && overrides[m.OldKey].Mask==256);
        });
    }
}
