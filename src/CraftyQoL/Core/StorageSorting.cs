using System;
using System.Collections.Generic;
using System.IO;

namespace CraftyQoL.Core
{
    internal enum StorageCategory { Materials=1, Metals, Food, Potions, Weapons, Armour, Tools, Ammunition, Valuables, Miscellaneous }
    internal static class StorageMetadata
    {
        internal static string Key(string prefab,int quality,int variant,int world,float durability,bool picked,bool equipped,bool cheated,
            long crafter,string crafterName,float lastAttack,bool projectile,IReadOnlyDictionary<string,string> custom)
        {
            if(prefab==null || crafterName==null || custom==null || custom.Count>4096)throw new InvalidDataException("Invalid item metadata.");
            long chars=(long)prefab.Length+crafterName.Length;
            foreach(var pair in custom){if(pair.Key==null || pair.Value==null)throw new InvalidDataException("Null custom item data.");chars+=(long)pair.Key.Length+pair.Value.Length;}
            if(chars>16000)throw new InvalidDataException("Item metadata exceeds sorting bound.");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(prefab);writer.Write(quality);writer.Write(variant);writer.Write(world);writer.Write(durability);
                writer.Write(picked);writer.Write(equipped);writer.Write(cheated);writer.Write(crafter);writer.Write(crafterName);writer.Write(lastAttack);writer.Write(projectile);
                var keys=new List<string>(custom.Keys);keys.Sort(StringComparer.Ordinal);writer.Write(keys.Count);
                foreach(string key in keys){if(key==null || custom[key]==null)throw new InvalidDataException("Null custom item data.");writer.Write(key);writer.Write(custom[key]);}
                if(stream.Length>49152)throw new InvalidDataException("Item metadata exceeds sorting bound.");
                return Convert.ToBase64String(stream.ToArray());
            }
        }
    }
    internal sealed class StorageItem
    {
        internal readonly int Index,Count,Maximum,X,Y;
        internal readonly string Id,Metadata;
        internal readonly StorageCategory Category;
        internal readonly bool Merge;
        internal readonly object Prefab,Shared;
        internal StorageItem(int index,string id,string metadata,StorageCategory category,int count,int maximum,int x,int y,bool merge,object prefab,object shared)
        {Index=index;Id=id;Metadata=metadata;Category=category;Count=count;Maximum=maximum;X=x;Y=y;Merge=merge;Prefab=prefab;Shared=shared;}
    }
    internal readonly struct StoragePlacement
    {
        internal readonly int Source,Count,X,Y;
        internal StoragePlacement(int source,int count,int x,int y){Source=source;Count=count;X=x;Y=y;}
    }
    internal sealed class StoragePlan
    {
        internal readonly List<StoragePlacement> Items;
        internal readonly bool Changed;
        internal StoragePlan(List<StoragePlacement> items,bool changed){Items=items;Changed=changed;}
    }
    internal static class StoragePlanner
    {
        internal const int MaxItems=4096;
        internal static StorageCategory Category(int type,int value,EquipmentFlags flags,bool metal,bool food,bool potion)
        {
            if(potion)return StorageCategory.Potions;
            if(food)return StorageCategory.Food;
            if(metal)return StorageCategory.Metals;
            if((flags & EquipmentFlags.Ammunition)!=0)return StorageCategory.Ammunition;
            if((flags & (EquipmentFlags.Armour|EquipmentFlags.Shields))!=0)return StorageCategory.Armour;
            if((flags & EquipmentFlags.Tools)!=0 || type==19 || type==15)return StorageCategory.Tools;
            if((flags & EquipmentFlags.Weapons)!=0)return StorageCategory.Weapons;
            if(type==13 || value>0)return StorageCategory.Valuables;
            return type==1?StorageCategory.Materials:StorageCategory.Miscellaneous;
        }
        private static int Compare(StorageItem a,StorageItem b)
        {
            int c=a.Category.CompareTo(b.Category);if(c!=0)return c;
            c=StringComparer.Ordinal.Compare(a.Id,b.Id);if(c!=0)return c;
            c=StringComparer.Ordinal.Compare(a.Metadata,b.Metadata);if(c!=0)return c;
            c=a.Maximum.CompareTo(b.Maximum);if(c!=0)return c;
            c=a.Y.CompareTo(b.Y);if(c!=0)return c;c=a.X.CompareTo(b.X);return c!=0?c:a.Index.CompareTo(b.Index);
        }
        private static string Key(StorageItem item)=>((int)item.Category)+":"+item.Id.Length+":"+item.Id+":"+item.Maximum+":"+item.Metadata;
        private static bool Compatible(StorageItem a,StorageItem b)=>a.Merge && b.Merge &&
            ReferenceEquals(a.Prefab,b.Prefab) && ReferenceEquals(a.Shared,b.Shared);
        private static void Check(IReadOnlyList<StorageItem> source,int width,int height)
        {
            if(width<1 || height<1 || width>255 || height>255 || source.Count>MaxItems || source.Count>width*height)throw new InvalidDataException("Unsupported storage capacity.");
            var slots=new HashSet<int>();
            for(int i=0;i<source.Count;i++)
            {
                var item=source[i];
                if(item==null || item.Index!=i || string.IsNullOrEmpty(item.Id) || item.Id.Length>1024 || item.Metadata==null || item.Metadata.Length>65536 ||
                    item.Prefab==null || item.Shared==null || item.Count<1 || item.Count>item.Maximum || item.Maximum<1 || item.Maximum>ushort.MaxValue ||
                    item.X<0 || item.Y<0 || item.X>=width || item.Y>=height || (int)item.Category<1 || (int)item.Category>10 || !slots.Add(item.Y*width+item.X))
                    throw new InvalidDataException("Invalid item metadata, stack or occupied slot.");
            }
        }
        internal static StoragePlan Build(IReadOnlyList<StorageItem> source,int width,int height)
        {
            Check(source,width,height);var sorted=new List<StorageItem>(source);sorted.Sort(Compare);
            var rows=new List<StoragePlacement>();var targets=new Dictionary<string,List<int>>(StringComparer.Ordinal);
            foreach(var item in sorted)
            {
                int remaining=item.Count;string key=Key(item);
                if(item.Merge && targets.TryGetValue(key,out var matches))
                    foreach(int at in matches)
                    {
                        var row=rows[at];if(!Compatible(item,source[row.Source]))continue;
                        int take=Math.Min(remaining,item.Maximum-row.Count);if(take==0)continue;
                        rows[at]=new StoragePlacement(row.Source,row.Count+take,row.X,row.Y);remaining-=take;if(remaining==0)break;
                    }
                if(remaining==0)continue;
                int slot=rows.Count;rows.Add(new StoragePlacement(item.Index,remaining,slot%width,slot/width));
                if(item.Merge){if(!targets.TryGetValue(key,out var entries))targets.Add(key,entries=new List<int>());entries.Add(slot);}
            }
            bool changed=rows.Count!=source.Count;
            for(int i=0;i<rows.Count;i++){var row=rows[i];var before=source[row.Source];changed|=row.Source!=i || row.Count!=before.Count || row.X!=before.X || row.Y!=before.Y;}
            var plan=new StoragePlan(rows,changed);Validate(source,plan,width,height);return plan;
        }
        internal static void Validate(IReadOnlyList<StorageItem> source,StoragePlan plan,int width,int height)
        {
            Check(source,width,height);
            var groups=new Dictionary<string,List<int>>(StringComparer.Ordinal);var groupFor=new int[source.Count];var samples=new List<int>();var before=new List<long>();
            for(int i=0;i<source.Count;i++)
            {
                var item=source[i];string key=Key(item);int group=-1;
                if(item.Merge && groups.TryGetValue(key,out var candidates))foreach(int g in candidates)if(Compatible(item,source[samples[g]])){group=g;break;}
                if(group<0){group=samples.Count;samples.Add(i);before.Add(0);if(item.Merge){if(!groups.TryGetValue(key,out var entries))groups.Add(key,entries=new List<int>());entries.Add(group);}}
                groupFor[i]=group;before[group]=checked(before[group]+item.Count);
            }
            var after=new long[before.Count];var used=new HashSet<int>();
            for(int i=0;i<plan.Items.Count;i++)
            {
                var row=plan.Items[i];
                if(row.Source<0 || row.Source>=source.Count || !used.Add(row.Source) || row.X!=i%width || row.Y!=i/width || row.Y>=height)throw new InvalidDataException("Invalid proposed storage layout.");
                var original=source[row.Source];
                if(row.Count<1 || row.Count>original.Maximum || !original.Merge && row.Count!=original.Count)throw new InvalidDataException("Invalid proposed stack.");
                if(i>0 && Compare(source[plan.Items[i-1].Source],original)>0)throw new InvalidDataException("Non-deterministic storage order.");
                after[groupFor[row.Source]]=checked(after[groupFor[row.Source]]+row.Count);
            }
            for(int i=0;i<before.Count;i++)if(before[i]!=after[i])throw new InvalidDataException("Storage quantity or metadata conservation failed.");
        }
    }
    internal static class StorageGeometry
    {
        private static bool Overlap(UiBox a,UiBox b)=>a.X<b.Right && b.X<a.Right && a.Y<b.Bottom && b.Y<a.Bottom;
        internal static bool Place(float width,float height,UiBox title,UiBox stack,UiBox take,UiBox weight,out UiBox button)
        {
            float left=title.Right+4,right=stack.X-4;button=new UiBox((left+right-44)/2,stack.Y,44,stack.Height);
            return !float.IsNaN(width) && !float.IsNaN(height) && !float.IsNaN(stack.Height) && !float.IsInfinity(stack.Height) && stack.Height>0 &&
                right-left>=44 && button.X>=2 && button.Right<=width-2 &&
                button.Y>=0 && button.Bottom<=height && !Overlap(button,title) && !Overlap(button,stack) && !Overlap(button,take) && !Overlap(button,weight);
        }
    }
    internal enum StoragePublication { Unchanged, Committed, Unknown }
    internal static class StorageReceipt
    {
        internal static StoragePublication State(bool expected,bool original,bool revisionChanged,bool nativeReceipt,bool notified,bool samePacket)
            =>expected && nativeReceipt && (revisionChanged || notified && samePacket)?StoragePublication.Committed:
                original?StoragePublication.Unchanged:StoragePublication.Unknown;
    }
    internal enum StorageResult { Unchanged, Aborted, Applied, Indeterminate }
    internal static class StorageTransaction
    {
        internal static StorageResult Execute(bool changed,Func<bool> begin,Action apply,Func<bool> ready,Action publish,
            Func<StoragePublication> publication,Func<bool> validApplied,Func<bool> canRollback,Action rollback,Func<bool> restored)
        {
            bool started=false;
            try
            {
                if(!begin())return StorageResult.Aborted;
                if(!changed)return StorageResult.Unchanged;
                started=true;apply();if(!ready())throw new InvalidOperationException("Storage changed before publication.");
                publish();if(publication()!=StoragePublication.Committed || !validApplied())throw new InvalidOperationException("Storage publication could not be confirmed.");
                return StorageResult.Applied;
            }
            catch
            {
                if(!started)return StorageResult.Aborted;
                try
                {
                    var state=publication();
                    if(state==StoragePublication.Committed && validApplied())return StorageResult.Applied;
                    if(state==StoragePublication.Unchanged && canRollback()){rollback();if(restored())return StorageResult.Aborted;}
                }
                catch { }
                return StorageResult.Indeterminate;
            }
        }
    }
}
