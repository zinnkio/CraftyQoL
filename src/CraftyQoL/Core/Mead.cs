using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    [Flags] internal enum MeadFlags { None=0, Healing=1, Stamina=2, Eitr=4, Resistance=8, Other=16 }
    internal enum MeadCategory { All, Healing, Stamina, Eitr, Resistance, Other }
    internal static class MeadEffects
    {
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        internal static double Positive(params double[] values)
        { double best=0;foreach(double v in values)if(Finite(v) && v>best)best=v;return best; }
        internal static double Reduction(params double[] values)
        { double best=0;foreach(double v in values)if(Finite(v) && v<best)best=v;return best; }
        internal static bool Resistant(int modifier) => modifier==1 || modifier==3 || modifier==5 || modifier==7;
        internal static MeadFlags Classify(double health,double healthRegen,double stamina,double staminaRegen,double eitr,double eitrRegen,bool resistance,bool utility=false)
        {
            MeadFlags flags=((Finite(health) && health>0 || Finite(healthRegen) && healthRegen>1)?MeadFlags.Healing:0)
                | ((Finite(stamina) && stamina>0 || Finite(staminaRegen) && staminaRegen>1)?MeadFlags.Stamina:0)
                | ((Finite(eitr) && eitr>0 || Finite(eitrRegen) && eitrRegen>1)?MeadFlags.Eitr:0)
                | (resistance?MeadFlags.Resistance:0);
            return flags | (utility || flags==MeadFlags.None?MeadFlags.Other:0);
        }
    }
    internal static class MeadQuery
    {
        internal static List<RecipeInfo> Filter(IReadOnlyList<RecipeInfo> source,QueryState query,ISet<string> favourites)
        {
            string search=RecipeInfo.Normalize(query.Search.Trim());
            MeadFlags category=query.MeadCategory==MeadCategory.All?MeadFlags.None:(MeadFlags)(1<<((int)query.MeadCategory-1));
            var result=new List<RecipeInfo>(source.Count);
            foreach(var info in source)
                if((category==MeadFlags.None || (info.Mead&category)!=0) && RecipeQuery.MatchesCommon(info,query,favourites,search,true))result.Add(info);
            return result;
        }
    }
    internal sealed class MeadGraph
    {
        private readonly Dictionary<string,MeadFlags> _direct=new Dictionary<string,MeadFlags>(StringComparer.Ordinal);
        private readonly Dictionary<string,List<string>> _finish=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        internal void Add(string id,MeadFlags flags)=>_direct[id]=flags;
        internal void Convert(string from,string to)
        {if(!_finish.TryGetValue(from,out var targets))_finish.Add(from,targets=new List<string>());if(!targets.Contains(to))targets.Add(to);}
        internal bool Ferments(string id)=>_finish.ContainsKey(id);
        internal MeadFlags Resolve(string id)
        {int remaining=128;return Visit(id,new HashSet<string>(StringComparer.Ordinal),0,ref remaining)??MeadFlags.Other;}
        private MeadFlags? Visit(string id,HashSet<string> path,int depth,ref int remaining)
        {
            if(depth>8 || --remaining<0 || !path.Add(id))return null;
            try
            {
                if(!_finish.TryGetValue(id,out var targets) || targets.Count==0)
                    return _direct.TryGetValue(id,out MeadFlags direct)?direct:(MeadFlags?)null;
                MeadFlags? common=null;
                foreach(string target in targets)
                {var value=Visit(target,path,depth+1,ref remaining);if(!value.HasValue || common.HasValue && common.Value!=value.Value)return null;common=value;}
                return common;
            }
            finally{path.Remove(id);}
        }
    }
    internal static class TrackerUiPolicy
    {
        internal static bool Visible(bool allowed,bool hasPins,bool inventoryOpen,bool keepVisible)=>allowed && hasPins && (inventoryOpen || keepVisible);
        internal static bool CanEdit(bool inventoryOpen)=>inventoryOpen;
    }
    internal static class TrackerPinGeometry
    {
        internal const float Size=32, Gap=4, Reserve=Size+Gap*2;
        internal static bool Fits(float craftWidth)=>!float.IsNaN(craftWidth) && !float.IsInfinity(craftWidth) && craftWidth>=Reserve+96;
    }
}
