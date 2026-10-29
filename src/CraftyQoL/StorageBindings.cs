using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using CraftyQoL.Core;
using HarmonyLib;

namespace CraftyQoL
{
    internal sealed class StorageBindings
    {
        internal readonly FieldInfo Current,View,Loading,Weight,Temporary,CheatedPopup,LastRevision;
        internal readonly MethodInfo NativeSave;
        internal readonly Func<Container,long,bool> Access;
        internal readonly Action<Inventory,bool,bool> Notify;
        internal readonly Func<InventoryGrid,Vector2i> Selection;
        internal readonly int ItemsKey;
        private readonly MethodBase[] _critical;
        private Container? _saveContainer;
        private Action? _saveDelegate;
        private static IEnumerable<Type> Deep(Type type)
        {var types=new List<Type>{type};foreach(var child in type.GetNestedTypes(BindingFlags.Public|BindingFlags.NonPublic))types.AddRange(Deep(child));return types;}
        internal void ClearTarget(){_saveContainer=null;_saveDelegate=null;}
        private static FieldInfo Field(Type owner,string name,Type type)
        {
            var field=AccessTools.DeclaredField(owner,name);
            if(field==null || field.FieldType!=type)throw new NotSupportedException(owner.Name+"."+name+" changed.");
            return field;
        }
        internal StorageBindings()
        {
            Current=Field(typeof(InventoryGui),"m_currentContainer",typeof(Container));View=Field(typeof(Container),"m_nview",typeof(ZNetView));
            Loading=Field(typeof(Container),"m_loading",typeof(bool));LastRevision=Field(typeof(Container),"m_lastRevision",typeof(uint));Weight=Field(typeof(Inventory),"m_totalWeight",typeof(float));
            Temporary=Field(typeof(Inventory),"m_temoraryInventory",typeof(bool));CheatedPopup=Field(typeof(Inventory),"m_cheatedPopup",typeof(bool));
            NativeSave=AccessTools.DeclaredMethod(typeof(Container),"OnContainerChanged")??throw new MissingMethodException("Container.OnContainerChanged");
            var access=AccessTools.DeclaredMethod(typeof(Container),"CheckAccess",new[]{typeof(long)})??throw new MissingMethodException("Container.CheckAccess");
            Access=(Func<Container,long,bool>)Delegate.CreateDelegate(typeof(Func<Container,long,bool>),access);
            var changed=AccessTools.DeclaredMethod(typeof(Inventory),"Changed",new[]{typeof(bool),typeof(bool)})??throw new MissingMethodException("Inventory.Changed");
            Notify=(Action<Inventory,bool,bool>)Delegate.CreateDelegate(typeof(Action<Inventory,bool,bool>),changed);
            var selection=AccessTools.DeclaredProperty(typeof(InventoryGrid),"SelectionGridPosition")?.GetGetMethod(true)??throw new MissingMethodException("InventoryGrid.SelectionGridPosition");
            Selection=(Func<InventoryGrid,Vector2i>)Delegate.CreateDelegate(typeof(Func<InventoryGrid,Vector2i>),selection);
            var vars=AccessTools.TypeByName("ZDOVars")??throw new MissingMemberException("ZDOVars");
            var key=Field(vars,"s_items",typeof(int));if(!key.IsStatic)throw new NotSupportedException("Storage ZDO key changed.");ItemsKey=(int)key.GetValue(null);
            string[] schema={"m_stack","m_durability","m_quality","m_variant","m_worldLevel","m_pickedUp","m_shared","m_crafterID","m_crafterName","m_customData","m_gridPos","m_equipped","m_cheated","m_dropPrefab","m_lastAttackTime","m_lastProjectile"};
            var fields=typeof(ItemDrop.ItemData).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Select(f=>f.Name).OrderBy(n=>n,StringComparer.Ordinal);
            if(!fields.SequenceEqual(schema.OrderBy(n=>n,StringComparer.Ordinal)))throw new NotSupportedException("Unknown ItemData fields; storage sorting disabled safely.");
            var extra=AccessTools.TypeByName("ZDOExtraData")??throw new MissingMemberException("ZDOExtraData");
            _critical=new[]{typeof(Inventory),typeof(Container),typeof(ItemDrop.ItemData),typeof(InventoryGrid),typeof(ZNetView),typeof(ZDO),typeof(ZDOMan),typeof(PrivateArea),extra,
                typeof(StoragePlanner),typeof(StorageTransaction),typeof(StorageSnapshot),typeof(StorageSorting),typeof(StorageBindings)}.SelectMany(Deep).SelectMany(t=>t.GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(t.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))).ToArray();
        }
        internal bool SingleNativeSave(Container container,Inventory inventory)
        {
            if(_saveContainer!=container){_saveContainer=container;_saveDelegate=(Action)Delegate.CreateDelegate(typeof(Action),container,NativeSave);}
            return inventory.m_onChanged!=null && inventory.m_onChanged.Equals(_saveDelegate);
        }
        internal string? Conflict()
        {
            foreach(var method in _critical)
            {var patches=Harmony.GetPatchInfo(method);if(patches!=null && patches.Owners.Any(owner=>owner!=Plugin.Id))return "Another mod changes storage/inventory authority, layout or serialisation.";}
            return null;
        }
    }
}
