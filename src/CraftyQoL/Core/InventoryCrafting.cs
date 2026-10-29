using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal enum RecipeFamily { All, Equipment, Food, Mead, Other }
    [Flags] internal enum CraftAccess { None=0, NoCost=1, NoCraftCost=2, AllRecipesUnlocked=4 }
    internal sealed class InventoryDisclosure
    {
        private bool? _manual;
        private bool _automatic;
        internal bool Expanded => _manual ?? _automatic;
        internal void Observe(CraftAccess access) => _automatic=(access & (CraftAccess.NoCost|CraftAccess.NoCraftCost))!=0;
        internal void Toggle() => _manual=!Expanded;
    }
    internal static class InventoryRules
    {
        internal const string Origin="@inventory";
        internal static bool CreativeKnowledge(CraftAccess access)=>(access & (CraftAccess.NoCost|CraftAccess.AllRecipesUnlocked))!=0;
        internal static bool CreativePin(bool inventoryOrigin,CraftAccess access,bool nativeAvailable)=>inventoryOrigin && CreativeKnowledge(access) && nativeAvailable;
        internal static bool HasBiomes(RecipeFamily family)=>family==RecipeFamily.Food || family==RecipeFamily.Equipment;
        internal static RecipeFamily Family(bool foodStation,bool meadStation,bool fermented,bool potion,FoodStats food,EquipmentFlags equipment,bool equipmentItem=false)
            => meadStation || fermented || potion ? RecipeFamily.Mead : foodStation || food.Categories!=FoodFlags.None ? RecipeFamily.Food :
               ((equipment & ~EquipmentFlags.Other)!=EquipmentFlags.None || equipmentItem) ? RecipeFamily.Equipment : RecipeFamily.Other;
        internal static bool EquipmentType(int type)=>type==3 || type==4 || type==5 || type==6 || type==7 || type==9 || type==11 || type==12 || type==14 || type==15 || type==17 || type==18 || type==19 || type==22 || type==23 || type==24;
        internal static bool Active(QueryState query)=>query.Family!=RecipeFamily.All || query.FavouritesOnly || query.Search.Trim().Length>0 || query.EquipmentSort!=EquipmentSort.Vanilla;
        internal static List<RecipeInfo> Filter(IReadOnlyList<RecipeInfo> source,QueryState query,ISet<string> favourites)
        {
            string search=RecipeInfo.Normalize(query.Search.Trim());
            var result=new List<RecipeInfo>(source.Count);
            foreach(var info in source)
            {
                if(query.Family!=RecipeFamily.All && info.Family!=query.Family)continue;
                if(query.Family==RecipeFamily.Equipment && query.EquipmentCategory!=EquipmentCategory.All && (info.Equipment & EquipmentClassifier.Flag(query.EquipmentCategory))==0)continue;
                if(query.Family==RecipeFamily.Food && query.Category!=FoodCategory.All && (info.Food.Categories & (FoodFlags)(1<<((int)query.Category-1)))==0)continue;
                if(query.Family==RecipeFamily.Mead && query.MeadCategory!=MeadCategory.All && (info.Mead & (MeadFlags)(1<<((int)query.MeadCategory-1)))==0)continue;
                if(RecipeQuery.MatchesCommon(info,query,favourites,search,!HasBiomes(query.Family)))result.Add(info);
            }
            return result;
        }
    }
    internal readonly struct InventoryToolbarGeometry
    {
        private readonly ToolbarGeometry _base;
        private readonly RecipeFamily _family;
        private readonly bool _expanded,_biomes;
        internal InventoryToolbarGeometry(float width,RecipeFamily family,bool expanded,bool biomes)
        { _base=new ToolbarGeometry(width);_family=family;_expanded=expanded;_biomes=biomes && InventoryRules.HasBiomes(family); }
        private int Rows=>_family==RecipeFamily.Equipment || _family==RecipeFamily.Mead?2:_family==RecipeFamily.Food?1:0;
        internal UiBox Toggle=>new UiBox(2,2,_base.ContentWidth-2*(_base.SelectionSize+3),22);
        internal UiBox Sort(int i)=>new UiBox(Toggle.Right+3+i*(_base.SelectionSize+3),2,_base.SelectionSize,_base.SelectionSize);
        internal UiBox Search=>new UiBox(_base.Search.X,27,_base.Search.Width,_base.Search.Height);
        internal UiBox Clear=>new UiBox(_base.Clear.X,27,_base.Clear.Width,_base.Clear.Height);
        internal UiBox Favourites=>new UiBox(_base.Favourites.X,27,_base.Favourites.Width,_base.Favourites.Height);
        internal UiBox Family(int i)=>new UiBox(2+i*((_base.ContentWidth-12)/5+3),52,(_base.ContentWidth-12)/5,22);
        internal UiBox Category(int i,RecipeFamily family)
        {int columns=family==RecipeFamily.Mead?3:4;float w=(_base.ContentWidth-(columns-1)*3)/columns;return new UiBox(2+i%columns*(w+3),77+i/columns*25,w,22);}
        private float BiomeY=>77+Rows*25;
        internal UiBox Disclosure=>new UiBox(2,BiomeY,_base.ContentWidth-33,22);
        internal UiBox Reset=>new UiBox(_base.Reset.X,BiomeY,30,22);
        internal UiBox Biome(int i)=>new UiBox(_base.Biome(i).X,BiomeY+25+i/5*25,_base.SelectionSize,_base.SelectionSize);
        internal float SeparatorY=>!_expanded?27:InventoryRules.HasBiomes(_family)?BiomeY+25+(_biomes?50:0):77+Rows*25;
        internal float Height=>SeparatorY+3;
    }
}
