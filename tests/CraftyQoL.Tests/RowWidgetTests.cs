using System;
using System.Linq;
using CraftyQoL.Core;
using Mono.Cecil;

internal static class RowWidgetTests
{
    internal static void Run(Action<string,Action> check)
    {
        check("row action dispatches only current recipe lease",()=>
        {var b=new RowClickBinding();int a=0,c=0;b.Bind(()=>a++);long old=b.Version;b.Bind(()=>c++);b.Invoke(old);Program.Assert(a==0 && c==0);b.Invoke(b.Version);Program.Assert(c==1);});
        check("released row action cannot toggle a favourite",()=>
        {var b=new RowClickBinding();int n=0;b.Bind(()=>n++);long press=b.Version;b.Clear();b.Invoke(press);b.Invoke(b.Version);Program.Assert(n==0);});
        check("ambiguous row identity keeps action unavailable",()=>
        {var b=new RowClickBinding();int n=0;b.Bind(()=>n++);b.Bind(null);b.Invoke(b.Version);Program.Assert(n==0);});
        check("separate pooled rows keep independent recipe actions",()=>
        {var a=new RowClickBinding();var b=new RowClickBinding();int x=0,y=0;a.Bind(()=>x++);b.Bind(()=>y++);a.Clear();b.Invoke(b.Version);a.Invoke(a.Version);Program.Assert(x==0 && y==1);});
        check("rebind inside action never replays obsolete press",()=>
        {var b=new RowClickBinding();int x=0,y=0;b.Bind(()=>{x++;b.Bind(()=>y++);});long press=b.Version;b.Invoke(press);b.Invoke(press);Program.Assert(x==1 && y==0);b.Invoke(b.Version);Program.Assert(y==1);});
    }
    internal static void Contracts(string plugin)
    {
        using var a=AssemblyDefinition.ReadAssembly(plugin);var m=a.MainModule;
        MethodDefinition Method(string type,string name)=>m.GetType("CraftyQoL."+type).Methods.Single(x=>x.Name==name);
        bool Calls(MethodDefinition body,string type,string name)=>body.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.DeclaringType.Name==type && c.Name==name);
        var prefix=Method("RecipeListPatch","Prefix").Body.Instructions.ToList();
        int release=prefix.FindIndex(i=>i.Operand is MethodReference c && c.Name=="BeforeRowsRebuilt");
        int filter=prefix.FindIndex(i=>i.Operand is MethodReference c && c.Name=="Filter");
        Program.Assert(release>=0 && release<filter,"Detach owned widgets in existing prefix before vanilla row destruction");
        Program.Assert(Calls(Method("FoodView","BeforeRowsRebuilt"),"FoodView","ClearDecorations"));
        Program.Assert(Calls(Method("RowDecoration","Dispose"),"RowWidgetPool","Return"));
        Program.Assert(Calls(Method("RowWidgets","Release"),"RowClickBinding","Clear"),"Drop captured recipe callbacks on release");
        Program.Assert(Calls(Method("RowWidgetPool","Rent"),"Stack`1","Pop"));
        Program.Assert(Calls(Method("RowWidgetPool","Return"),"Stack`1","Push"));
        Program.Assert(Calls(Method("FoodView","CloseOwned"),"RowWidgetPool","Dispose"));
        var pool=m.GetType("CraftyQoL.RowWidgetPool");
        Program.Assert((int)pool.Fields.Single(f=>f.Name=="RetainedLimit").Constant==512,"Bound retained controls");
        Program.Assert(!pool.Methods.Any(x=>x.Name is "Update" or "LateUpdate"));
        Program.Assert(!Calls(Method("RowDecoration",".ctor"),"NativeToolbar","MakeButton"),"Never clone/strip full native tabs per row");
        var decoration=m.GetType("CraftyQoL.RowDecoration");
        Program.Assert(!decoration.Methods.Where(x=>x.HasBody).SelectMany(x=>x.Body.Instructions).Any(i=>
            i.Operand is MethodReference c && (c.DeclaringType.Name=="CraftButtonStyle" || c.DeclaringType.Name=="UiStyle" && c.Name=="Round")),
            "Native recipe-row backgrounds/highlights must not acquire rounding");
        Program.Assert(!decoration.Fields.Any(f=>f.FieldType.FullName.Contains("RoundedImage")),"No unused per-row rounding state");
        Program.Assert(Calls(Method("RowWidgetPool",".ctor"),"UiStyle","Round"),"Favourite buttons stay rounded");
        var makeButton=m.GetType("CraftyQoL.NativeToolbar").Methods.Single(x=>x.Name=="MakeButton" && x.Parameters.Count==9);
        Program.Assert(Calls(makeButton,"UiStyle","Round"),"Filter buttons stay rounded");
        Program.Assert(Calls(makeButton,"UiStyle","ButtonSkin") && Calls(makeButton,"UiStyle","ButtonText"),"Toolbar/tracker actions share native visuals");
        Program.Assert(Calls(Method("RowWidgetPool",".ctor"),"UiStyle","ButtonSkin"),"Cached row favourites use the same native skin");
        Program.Assert(Calls(Method("TrackerPinControl",".ctor"),"UiStyle","ButtonSkin"),"Craft pin can match its adjacent native action");
        var skin=Method("UiStyle","ButtonSkin");
        foreach(string property in new[]{"spriteState","transition","colors"})
            Program.Assert(skin.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.Name=="get_"+property) &&
                skin.Body.Instructions.Any(i=>i.Operand is MethodReference c && c.Name=="set_"+property),"Keep native interaction visuals: "+property);
        Program.Assert(!skin.Body.Instructions.Any(i=>i.Operand is MethodReference c && (c.Name=="Instantiate" || c.Name=="get_onClick" || c.Name=="AddListener")),
            "Skin copying must not clone native action callbacks");
        Program.Assert(!Method("UiStyle","Selected").Body.Instructions.Any(i=>i.Operand is MethodReference c && (c.Name=="set_colors" || c.Name=="set_transition" || c.Name=="set_spriteState")),
            "Filter/pin selection must retain native hover, pressed and disabled states");
        Program.Assert(Calls(Method("RowFavouriteButton","OnPointerClick"),"RowClickBinding","get_Version"),"Mouse click must reject old binding generation");
        Program.Assert(m.GetType("CraftyQoL.RowFavouriteButton").BaseType.FullName=="UnityEngine.UI.Button");
        Program.Assert(!m.GetType("CraftyQoL.RowFavouriteButton").Methods.Any(x=>x.Name=="OnSubmit"),"Keep native keyboard/controller submission");
        foreach(var type in new[]{pool,m.GetType("CraftyQoL.RowWidgets"),m.GetType("CraftyQoL.RowDecoration"),m.GetType("CraftyQoL.RowFavouriteButton")})
        foreach(var method in type.Methods.Where(x=>x.HasBody))
        foreach(var call in method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>())
            Program.Assert(!new[]{"Player","Inventory","Recipe","Container","ZNetView","ZDO"}.Contains(call.DeclaringType.Name),"Row presentation must not call gameplay APIs: "+call.FullName);
    }
}
