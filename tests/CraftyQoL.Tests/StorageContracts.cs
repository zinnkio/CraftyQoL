using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class StorageContracts
{
    private static void Assert(bool condition,string message)=>Program.Assert(condition,message);
    private static IEnumerable<TypeDefinition> Deep(TypeDefinition type)
    {yield return type;foreach(var child in type.NestedTypes)foreach(var nested in Deep(child))yield return nested;}
    private static MethodDefinition Method(TypeDefinition type,string name,params string[] parameters)=>type.Methods.Single(m=>m.Name==name && m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameters));
    internal static void Native(string path)
    {
        using var assembly=AssemblyDefinition.ReadAssembly(path);var module=assembly.MainModule;
        var container=module.GetType("Container");var inventory=module.GetType("Inventory");var item=module.GetType("ItemDrop/ItemData");
        foreach(var f in new[]{("m_nview","ZNetView"),("m_loading","System.Boolean"),("m_lastRevision","System.UInt32")})Assert(container.Fields.Any(x=>x.Name==f.Item1 && x.FieldType.FullName==f.Item2 && x.IsPrivate),"Container private binding changed: "+f.Item1);
        Assert(module.GetType("InventoryGui").Fields.Any(f=>f.Name=="m_currentContainer" && f.IsPrivate && f.FieldType.FullName=="Container"),"Current container binding changed");
        foreach(string name in new[]{"GetInventory","IsOwner","IsInUse"})Assert(Method(container,name).IsPublic,"Native container API changed");
        Assert(Method(container,"CheckAccess","System.Int64").ReturnType.FullName=="System.Boolean","Access binding changed");
        var request=Method(container,"RPC_RequestOpen","System.Int64","System.Int64");var requestCalls=request.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(requestCalls.Any(m=>m.Name=="ForceSendZDO") && requestCalls.Any(m=>m.Name=="SetOwner") && requestCalls.Any(m=>m.Name=="CheckAccess"),"Native open/ownership protocol changed");
        var save=Method(container,"Save");var ops=save.Body.Instructions.ToArray();
        int set=Array.FindIndex(ops,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="ZDO" && m.Name=="Set");
        int receipt=Array.FindIndex(ops,i=>i.OpCode.Code==Code.Stfld && i.Operand is FieldReference f && f.Name=="m_lastRevision");
        Assert(set>=0 && receipt>set && ops.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="Inventory" && m.Name=="Save"),"Native save receipt/serialisation changed");
        Assert(Method(container,"OnContainerChanged").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Save"),"Container change no longer saves");
        var changed=Method(inventory,"Changed","System.Boolean","System.Boolean");Assert(changed.IsPrivate,"Inventory changed signature changed");
        Assert(changed.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="UpdateTotalWeight") && changed.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="m_onChanged"),"Native notification/weight path changed");
        Assert(Method(inventory,"GetAllItems").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="m_inventory"),"GetAllItems no longer exposes native list");
        foreach(string name in new[]{"m_totalWeight","m_cheatedPopup","m_temoraryInventory"})Assert(inventory.Fields.Any(f=>f.Name==name && f.IsPrivate),"Native journal binding changed: "+name);
        string[] fields={"m_stack","m_durability","m_quality","m_variant","m_worldLevel","m_pickedUp","m_shared","m_crafterID","m_crafterName","m_customData","m_gridPos","m_equipped","m_cheated","m_dropPrefab","m_lastAttackTime","m_lastProjectile"};
        Assert(item.Fields.Where(f=>!f.IsStatic).Select(f=>f.Name).OrderBy(x=>x).SequenceEqual(fields.OrderBy(x=>x)),"Unknown native ItemData fields");
        var clone=Method(item,"Clone");Assert(clone.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="MemberwiseClone"),"Native clone semantics changed");
        var itemSave=Method(item,"Save","ZPackage");foreach(string name in fields.Where(f=>f!="m_shared" && f!="m_lastAttackTime" && f!="m_lastProjectile"))Assert(itemSave.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name),"Native persistence stopped using "+name);
        Assert(Method(module.GetType("InventoryGrid"),"get_SelectionGridPosition").ReturnType.FullName=="Vector2i","Native grid selection binding changed");
        var data=module.GetType("ZDO");Method(data,"Set","System.Int32","System.Byte[]");Method(data,"GetByteArray","System.Int32","System.Byte[]");
        var revision=Method(data,"IncreaseDataRevision").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Assert(revision.Any(m=>m.Name=="ClientChanged") && revision.Any(m=>m.Name=="SetDirtySector"),"Native client replication/dirty path changed");
        Assert(module.GetType("ZDOVars").Fields.Any(f=>f.Name=="s_items" && f.IsStatic && f.FieldType.FullName=="System.Int32"),"Native items key changed");
    }
    internal static void Plugin(string path)
    {
        using var assembly=AssemblyDefinition.ReadAssembly(path);var module=assembly.MainModule;
        var names=new[]{"CraftyQoL.StorageSorting","CraftyQoL.StorageSnapshot","CraftyQoL.StorageBindings","CraftyQoL.StorageCatalogue"};
        var methods=names.SelectMany(n=>Deep(module.GetType(n))).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToArray();
        var calls=methods.SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        foreach(string forbidden in new[]{"ClaimOwnership","SetOwner","InvokeRPC","ForceSendZDO","Load","MoveAll","StackAll","MoveItemToThis","AddItem","RemoveItem","DropItem"})Assert(!calls.Any(m=>m.Name==forbidden),"Storage must not use "+forbidden);
        Assert(!calls.Any(m=>m.DeclaringType.Name=="ZDO" && m.Name=="Set"),"Only native inventory notification may publish storage data");
        Assert(!calls.Any(m=>m.DeclaringType.FullName.StartsWith("System.IO.File",StringComparison.Ordinal)),"Storage must not write save/recovery files");
        foreach(var method in methods)foreach(var op in method.Body.Instructions.Where(i=>i.OpCode.Code==Code.Stfld || i.OpCode.Code==Code.Stsfld))
            if(op.Operand is FieldReference field)Assert(!new[]{"Player","InventoryGui","Container","ZDO","ZNetView"}.Contains(field.DeclaringType.Name),"Storage wrote native authority/player field");
        var service=module.GetType("CraftyQoL.StorageSorting");var update=Method(service,"Update").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>();
        Assert(!update.Any(m=>m.Name=="Sort" || m.Name=="Execute" || m.Name=="Build" || m.DeclaringType.Name=="StorageSnapshot"),"Sorting must require explicit button press");
        Assert(Method(service,"Sort").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Execute" && m.DeclaringType.Name=="StorageTransaction"),"Sort bypasses validated transaction");
        Assert(calls.Any(m=>m.Name=="State" && m.DeclaringType.Name=="StorageReceipt"),"Native save receipt not checked");
        var snapshot=module.GetType("CraftyQoL.StorageSnapshot");
        foreach(string name in new[]{"Apply","Rollback"})foreach(var op in Method(snapshot,name).Body.Instructions.Where(i=>i.OpCode.Code==Code.Stfld))
            if(op.Operand is FieldReference field && field.DeclaringType.FullName=="ItemDrop/ItemData")Assert(field.Name=="m_stack" || field.Name=="m_gridPos","Journal overwrote item metadata");
        Assert(Method(snapshot,"PlayerAlias").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="GetInventory"),"Player/hotbar alias guard missing");
        Assert(calls.Any(m=>m.Name=="GetPatchInfo") && calls.Any(m=>m.Name=="SingleNativeSave"),"Foreign callback/patch qualification missing");
        var context=Method(service,"TryContext","CraftyQoL.StorageSorting/Context&","System.String&","System.Boolean");
        Assert(context.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="IsOwner") && context.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="CheckAccess"),"Authority/ward recheck missing");
        var entry=snapshot.NestedTypes.Single(t=>t.Name=="Entry");var merge=Method(entry,"get_Merge").Body.Instructions;
        Assert(merge.Any(i=>i.Operand is FieldReference f && f.Name=="m_autoStack") && merge.Any(i=>i.Operand is FieldReference f && f.Name=="m_equipped") && merge.Any(i=>i.Operand is FieldReference f && f.Name=="m_lastProjectile"),"Conservative runtime merge guards missing");
        Assert(!methods.Any(m=>m.CustomAttributes.Any(a=>a.AttributeType.Name=="AsyncStateMachineAttribute" || a.AttributeType.Name=="IteratorStateMachineAttribute")),"Storage mutation must remain synchronous");
    }
}
