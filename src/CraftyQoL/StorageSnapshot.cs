using System;
using System.Collections.Generic;
using System.IO;
using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal sealed class StorageSnapshot
    {
        internal const int MaxPacket=8*1024*1024;
        private sealed class Entry
        {
            internal readonly ItemDrop.ItemData Item,Before;
            internal readonly Dictionary<string,string> Custom;
            internal readonly string Metadata;
            internal readonly int Maximum;
            internal Entry(ItemDrop.ItemData item)
            {
                if(item==null || item.GetType()!=typeof(ItemDrop.ItemData) || !item.m_dropPrefab || item.m_shared==null || item.m_customData==null || item.m_customData.Count>4096 || item.m_crafterName==null ||
                    float.IsNaN(item.m_durability) || float.IsInfinity(item.m_durability) || item.m_durability<0 || item.m_durability>=int.MaxValue/100f ||
                    item.m_worldLevel<0 || item.m_worldLevel>255 || item.m_quality<1 || item.m_quality>ushort.MaxValue)
                    throw new InvalidDataException("Unsupported item type, fields or serialisable values.");
                string id=item.m_dropPrefab.name;if(string.IsNullOrEmpty(id) || id.Length>1024)throw new InvalidDataException("Item prefab identity is unsupported.");
                var prefab=ObjectDB.instance?ObjectDB.instance.GetItemPrefab(id):null;
                if(prefab!=item.m_dropPrefab || !prefab.GetComponent<ItemDrop>() || ReferenceEquals(item,prefab.GetComponent<ItemDrop>().m_itemData))throw new InvalidDataException("Item prefab is not uniquely registered.");
                Item=item;Before=item.Clone();Custom=item.m_customData;Maximum=item.m_shared.m_maxStackSize;Metadata=Key(item);
            }
            internal static string Key(ItemDrop.ItemData item)=>StorageMetadata.Key(item.m_dropPrefab.name,item.m_quality,item.m_variant,item.m_worldLevel,item.m_durability,
                item.m_pickedUp,item.m_equipped,item.m_cheated,item.m_crafterID,item.m_crafterName,item.m_lastAttackTime,!ReferenceEquals(item.m_lastProjectile,null),item.m_customData);
            internal bool SameMetadata()=>Item.GetType()==typeof(ItemDrop.ItemData) && ReferenceEquals(Item.m_shared,Before.m_shared) && ReferenceEquals(Item.m_dropPrefab,Before.m_dropPrefab) &&
                ReferenceEquals(Item.m_customData,Custom) && ReferenceEquals(Item.m_lastProjectile,Before.m_lastProjectile) && Item.m_shared.m_maxStackSize==Maximum && Key(Item)==Metadata;
            internal bool Merge=>Maximum>1 && Item.m_shared.m_autoStack && Custom.Count==0 && Custom.GetType()==typeof(Dictionary<string,string>) &&
                ReferenceEquals(Custom.Comparer,EqualityComparer<string>.Default) && !Item.m_equipped && Item.m_lastAttackTime==0 && ReferenceEquals(Item.m_lastProjectile,null);
        }
        private readonly Inventory _inventory;
        private readonly List<ItemDrop.ItemData> _list;
        private readonly Entry[] _entries;
        private readonly ItemDrop.ItemData[] _original,_retained;
        private readonly HashSet<ItemDrop.ItemData> _owned=new HashSet<ItemDrop.ItemData>();
        private readonly StorageBindings _bindings;
        private readonly int _width,_height;
        private readonly float _weight;
        private readonly int[] _counts,_xs,_ys;
        internal readonly byte[] Original,Expected;
        internal readonly StoragePlan Plan;
        internal StorageSnapshot(Inventory inventory,StorageBindings bindings,StorageCatalogue catalogue,byte[] saved)
        {
            _inventory=inventory;_bindings=bindings;_list=inventory.GetAllItems();_width=inventory.GetWidth();_height=inventory.GetHeight();_weight=inventory.GetTotalWeight();
            if(_list.Count>StoragePlanner.MaxItems || _width<1 || _height<1 || _width>255 || _height>255 || _list.Count>_width*_height ||
                float.IsNaN(_weight) || float.IsInfinity(_weight))throw new InvalidDataException("Storage capacity/weight is unsupported.");
            _original=_list.ToArray();_entries=new Entry[_original.Length];var descriptors=new List<StorageItem>(_original.Length);long metadataSize=0;
            for(int i=0;i<_original.Length;i++)
            {
                if(!_owned.Add(_original[i]))throw new InvalidDataException("Duplicate item reference.");
                var entry=new Entry(_original[i]);_entries[i]=entry;var item=entry.Item;
                metadataSize+=entry.Metadata.Length;if(metadataSize>MaxPacket*2L)throw new InvalidDataException("Storage metadata exceeds sorting bound.");
                descriptors.Add(new StorageItem(i,item.m_dropPrefab.name,entry.Metadata,catalogue.Category(item),item.m_stack,entry.Maximum,item.m_gridPos.x,item.m_gridPos.y,entry.Merge,item.m_dropPrefab,item.m_shared));
            }
            if(PlayerAlias())throw new InvalidDataException("Container item aliases player inventory.");
            Original=Serialise(inventory);if(!Equal(Original,saved))throw new InvalidDataException("Waiting for matching native container data.");
            Plan=StoragePlanner.Build(descriptors,_width,_height);
            _counts=new int[_entries.Length];_xs=new int[_entries.Length];_ys=new int[_entries.Length];
            for(int i=0;i<_entries.Length;i++){_xs[i]=_entries[i].Before.m_gridPos.x;_ys[i]=_entries[i].Before.m_gridPos.y;}
            _retained=new ItemDrop.ItemData[Plan.Items.Count];var staging=new Inventory(true);
            for(int i=0;i<Plan.Items.Count;i++)
            {
                var row=Plan.Items[i];var entry=_entries[row.Source];_counts[row.Source]=row.Count;_xs[row.Source]=row.X;_ys[row.Source]=row.Y;_retained[i]=entry.Item;
                var clone=entry.Before.Clone();clone.m_customData=entry.Custom;clone.m_stack=row.Count;clone.m_gridPos=new Vector2i(row.X,row.Y);staging.GetAllItems().Add(clone);
            }
            Expected=Serialise(staging);
        }
        internal static byte[] Serialise(Inventory inventory)
        {
            var package=new ZPackage();inventory.Save(package);var bytes=package.GetArray();
            if(bytes.Length>MaxPacket)throw new InvalidDataException("Container package exceeds sorting bound.");return bytes;
        }
        internal static bool Equal(byte[]? a,byte[]? b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private bool PlayerAlias()
        {
            if(!Player.m_localPlayer)return true;
            foreach(var item in Player.m_localPlayer.GetInventory().GetAllItems())if(_owned.Contains(item))return true;
            return false;
        }
        private bool SameInventory()=>!PlayerAlias() && ReferenceEquals(_inventory.GetAllItems(),_list) && _inventory.GetWidth()==_width && _inventory.GetHeight()==_height;
        internal bool OriginalState()
        {
            if(!SameInventory() || _list.Count!=_original.Length)return false;
            for(int i=0;i<_entries.Length;i++)
            {var e=_entries[i];if(!ReferenceEquals(_list[i],e.Item) || !e.SameMetadata() || e.Item.m_stack!=e.Before.m_stack || e.Item.m_gridPos.x!=e.Before.m_gridPos.x || e.Item.m_gridPos.y!=e.Before.m_gridPos.y)return false;}
            return Equal(Serialise(_inventory),Original);
        }
        internal void Apply()
        {
            for(int i=0;i<_entries.Length;i++){_entries[i].Item.m_stack=_counts[i];_entries[i].Item.m_gridPos=new Vector2i(_xs[i],_ys[i]);}
            _list.Clear();_list.AddRange(_retained);
        }
        internal bool AppliedState()
        {
            if(!SameInventory() || _list.Count!=_retained.Length)return false;
            for(int i=0;i<_retained.Length;i++)if(!ReferenceEquals(_list[i],_retained[i]))return false;
            for(int i=0;i<_entries.Length;i++)
            {var e=_entries[i];if(!e.SameMetadata() || e.Item.m_stack!=_counts[i] || e.Item.m_gridPos.x!=_xs[i] || e.Item.m_gridPos.y!=_ys[i])return false;}
            return Equal(Serialise(_inventory),Expected);
        }
        internal bool JournalOnly()
        {
            if(!SameInventory())return false;
            bool original=_list.Count==_original.Length;
            if(original)for(int i=0;i<_list.Count;i++)if(!ReferenceEquals(_list[i],_original[i])){original=false;break;}
            if(!original)
            {if(_list.Count>_retained.Length)return false;for(int i=0;i<_list.Count;i++)if(!ReferenceEquals(_list[i],_retained[i]))return false;}
            for(int i=0;i<_entries.Length;i++)
            {
                var e=_entries[i];if(!e.SameMetadata())return false;
                if(e.Item.m_stack!=e.Before.m_stack && e.Item.m_stack!=_counts[i])return false;
                bool old=e.Item.m_gridPos.x==e.Before.m_gridPos.x && e.Item.m_gridPos.y==e.Before.m_gridPos.y;
                if(!old && (e.Item.m_gridPos.x!=_xs[i] || e.Item.m_gridPos.y!=_ys[i]))return false;
            }
            return true;
        }
        internal void Rollback()
        {
            foreach(var entry in _entries){entry.Item.m_stack=entry.Before.m_stack;entry.Item.m_gridPos=entry.Before.m_gridPos;}
            _list.Clear();_list.AddRange(_original);_bindings.Weight.SetValue(_inventory,_weight);
        }
    }
}
