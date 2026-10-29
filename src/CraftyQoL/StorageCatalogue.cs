using System;
using System.Collections.Generic;
using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal sealed class StorageCatalogue
    {
        private ObjectDB? _db;private ZNetScene? _scene;private int _recipes,_prefabs;
        private readonly HashSet<string> _metals=new HashSet<string>(StringComparer.Ordinal),_food=new HashSet<string>(StringComparer.Ordinal),_potions=new HashSet<string>(StringComparer.Ordinal);
        internal void Invalidate(){_db=null;_scene=null;_food.Clear();_metals.Clear();_potions.Clear();}
        private void Ensure()
        {
            var db=ObjectDB.instance;var scene=ZNetScene.instance;
            if(!db || !scene)throw new InvalidOperationException("Storage item catalogue unavailable.");
            if(_db==db && _scene==scene && _recipes==db.m_recipes.Count && _prefabs==scene.m_prefabs.Count)return;
            _db=db;_scene=scene;_recipes=db.m_recipes.Count;_prefabs=scene.m_prefabs.Count;_metals.Clear();_food.Clear();_potions.Clear();
            var graph=new FoodGraph();var items=new List<ItemDrop>();
            foreach(var prefab in scene.m_prefabs)
            {
                if(!prefab)continue;var item=prefab.GetComponent<ItemDrop>();
                if(item && item.m_itemData?.m_shared!=null)
                {
                    items.Add(item);var shared=item.m_itemData.m_shared;
                    if(shared.m_itemType==ItemDrop.ItemData.ItemType.Consumable)
                    {
                        var stats=new FoodStats(shared.m_food,shared.m_foodStamina,shared.m_foodEitr);graph.AddEdible(prefab.name,stats);
                        if(shared.m_consumeStatusEffect && stats.Categories==FoodFlags.None)_potions.Add(prefab.name);
                    }
                }
                var cooking=prefab.GetComponent<CookingStation>();
                if(cooking && cooking.m_conversion!=null)foreach(var conversion in cooking.m_conversion)
                    if(conversion!=null && conversion.m_from && conversion.m_to)graph.AddFinish(conversion.m_from.gameObject.name,conversion.m_to.gameObject.name);
                var fermenter=prefab.GetComponent<Fermenter>();
                if(fermenter && fermenter.m_conversion!=null)foreach(var conversion in fermenter.m_conversion)
                    if(conversion!=null && conversion.m_from && conversion.m_to){_potions.Add(conversion.m_from.gameObject.name);_potions.Add(conversion.m_to.gameObject.name);}
                var feast=prefab.GetComponent<Feast>();var piece=prefab.GetComponent<Piece>();
                if(feast && piece && piece.m_resources!=null && piece.m_resources.Length==1 && piece.m_resources[0]!=null && piece.m_resources[0].m_resItem && piece.m_resources[0].m_amount==1)
                {var food=feast.m_foodItem?feast.m_foodItem:item;if(food)graph.AddFinish(piece.m_resources[0].m_resItem.gameObject.name,food.gameObject.name,true);}
            }
            foreach(var item in items)if(graph.Resolve(item.gameObject.name).Stats.Categories!=FoodFlags.None)_food.Add(item.gameObject.name);
            foreach(string id in new[]{"smelter","blastfurnace"})
            {
                var prefab=scene.GetPrefab(id);var smelter=prefab?prefab.GetComponent<Smelter>():null;
                if(smelter && smelter!.m_conversion!=null)foreach(var conversion in smelter.m_conversion)
                    if(conversion!=null && conversion.m_from && conversion.m_to){_metals.Add(conversion.m_from.gameObject.name);_metals.Add(conversion.m_to.gameObject.name);}
            }
            foreach(var recipe in db.m_recipes)
            {
                if(!recipe || !recipe.m_enabled || recipe.m_noCraftOnlyUpgrade || !recipe.m_item || !recipe.m_craftingStation || recipe.m_resources==null)continue;
                string station=recipe.m_craftingStation.gameObject.name;
                if(station!="piece_cauldron" && station!="piece_preptable" && !StationRules.IsMead(station))continue;
                if(StationRules.IsMead(station))_potions.Add(recipe.m_item.gameObject.name);else _food.Add(recipe.m_item.gameObject.name);
                foreach(var need in recipe.m_resources)if(need!=null && need.m_resItem && !need.m_upgraderResource && need.m_amount>0)_food.Add(need.m_resItem.gameObject.name);
            }
            for(int pass=0;pass<8;pass++)
            {
                bool added=false;var known=new HashSet<string>(_metals,StringComparer.Ordinal);
                foreach(var recipe in db.m_recipes)
                {
                    if(!recipe || !recipe.m_enabled || recipe.m_noCraftOnlyUpgrade || !recipe.m_item || recipe.m_item.m_itemData.m_shared.m_itemType!=ItemDrop.ItemData.ItemType.Material || recipe.m_resources==null || recipe.m_resources.Length==0)continue;
                    bool all=true,any=false;
                    foreach(var need in recipe.m_resources)
                    {if(need==null || !need.m_resItem){all=false;break;}if(need.m_upgraderResource || need.m_amount<=0)continue;any=true;if(!known.Contains(need.m_resItem.gameObject.name)){all=false;break;}}
                    if(all && any)added|=_metals.Add(recipe.m_item.gameObject.name);
                }
                if(!added)break;
            }
        }
        internal StorageCategory Category(ItemDrop.ItemData item)
        {
            Ensure();var shared=item.m_shared;string id=item.m_dropPrefab.name;var attack=shared.m_attack;
            var flags=EquipmentClassifier.Classify(id,(int)shared.m_itemType,(int)shared.m_skillType,shared.m_damages.m_chop,shared.m_damages.m_pickaxe,
                attack!=null && attack.m_attackType==Attack.AttackType.Projectile && attack.m_consumeItem,shared.m_eitrRegenModifier);
            var stats=new FoodStats(shared.m_food,shared.m_foodStamina,shared.m_foodEitr);
            bool edible=shared.m_itemType==ItemDrop.ItemData.ItemType.Consumable && stats.Categories!=FoodFlags.None;
            bool potion=shared.m_itemType==ItemDrop.ItemData.ItemType.Consumable && shared.m_consumeStatusEffect && !edible;
            return StoragePlanner.Category((int)shared.m_itemType,shared.m_value,flags,_metals.Contains(id),_food.Contains(id) || edible,_potions.Contains(id) || potion);
        }
    }
}
