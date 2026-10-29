using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    // Exact, asset-verified 1.0.20 -> 1.0.21 station moves. No fuzzy/localised identity matching.
    internal sealed class RecipeMove
    {
        internal const string OldStation="piece_cauldron", NewStation="piece_preptable";
        internal readonly string Recipe,Output,OldKey,NewKey,OldSignature,NewSignature;
        internal RecipeMove(string recipe,string output,string before,string after)
        {Recipe=recipe;Output=output;OldKey=RecipeInfo.StableKey(OldStation,recipe,output);NewKey=RecipeInfo.StableKey(NewStation,recipe,output);OldSignature=before;NewSignature=after;}
        internal bool Approves(string key,string signature,bool enabled,bool upgradeOnly,bool unique)=>unique && enabled && !upgradeOnly && key==NewKey && signature==NewSignature;
        internal bool MatchesOld(TrackerPin pin)=>pin.Key==OldKey && pin.Station==OldStation && pin.Signature==OldSignature;
    }
    internal static class RecipeMoves
    {
        internal static readonly IReadOnlyList<RecipeMove> All=new[]
        {
            new RecipeMove("Recipe_OvenPancake","OvenPancakeUncooked","D82BC072F27C8D1E9F9701CD5CB579D847ECF9AD7E9D16CF740954FEAD3E3BA8","E4DC2C8D418687F532605E46E15172C0E991423DD75A6327014F2E0DB60EDF4C"),
            new RecipeMove("Recipe_BakedPoteitr","BakedPoteitrUncooked","8582C22B9F191C51C0D7F424A27282E6DF4F3ADE0CC4E423EE8DA84A0F2595A6","05340C4C736CD86ADF286BEA4F1CD92C902A2CABE70BE8C33E63F36A3C48B7BF"),
            new RecipeMove("Recipe_KaleChips","KaleChipsUncooked","C62D9F88419691335184E4ACDBF8DBCC12D78C6273675668774E5E9892109333","79A551D68959B1E56B273DFE2EB682B615B74B3325D35EBA7BAE23B74BD36232")
        };
        internal static void AliasOverrides(Dictionary<string,BiomeAssociation> overrides,IReadOnlyList<RecipeMove> approved)
        {
            foreach(var move in approved)
                if(!overrides.ContainsKey(move.NewKey) && overrides.TryGetValue(move.OldKey,out var association))overrides.Add(move.NewKey,association);
        }
        internal static bool MigratePins(List<TrackerPin> pins,IReadOnlyList<RecipeMove> approved)
        {
            bool changed=false;
            foreach(var move in approved)
                for(int at=0;at<pins.Count;at++)
                {
                    var pin=pins[at];if(!move.MatchesOld(pin))continue;
                    int duplicate=pins.FindIndex(p=>p.Key==move.NewKey);
                    if(duplicate>=0)
                    {
                        var current=pins[duplicate];
                        if(current.Signature!=move.NewSignature || current.Station!=RecipeMove.NewStation)continue; // Unidentified conflict: preserve both records.
                        if(duplicate<at){current.Expanded|=pin.Expanded;pins.RemoveAt(at--);changed=true;continue;}
                        pin.Expanded|=current.Expanded;pins.RemoveAt(duplicate);
                    }
                    pin.Key=move.NewKey;pin.Station=RecipeMove.NewStation;pin.Signature=move.NewSignature;changed=true;
                }
            return changed;
        }
    }
}
