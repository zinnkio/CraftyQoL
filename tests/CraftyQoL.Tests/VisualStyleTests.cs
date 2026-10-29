using System;
using System.Collections.Generic;
using System.Linq;
using CraftyQoL.Core;

internal static class VisualStyleTests
{
    private readonly struct Vertex
    {
        internal readonly float X,Y,U,V,Tint;
        internal Vertex(float x,float y,float u,float v,float tint){X=x;Y=y;U=u;V=v;Tint=tint;}
    }
    private static Vertex Mix(Vertex a,Vertex b,float t)=>new Vertex(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.U+(b.U-a.U)*t,a.V+(b.V-a.V)*t,a.Tint+(b.Tint-a.Tint)*t);
    private static float Area(IEnumerable<UiPoint> points)
    {
        var p=points.ToArray();float sum=0;for(int i=0;i<p.Length;i++){var next=p[(i+1)%p.Length];sum+=p[i].X*next.Y-next.X*p[i].Y;}return sum/2;
    }
    private static bool Inside(UiPoint[] boundary,UiPoint p)
    {
        for(int i=0;i<boundary.Length;i++){var a=boundary[i];var b=boundary[(i+1)%boundary.Length];if((b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X)<-.001f)return false;}return true;
    }
    internal static void Run(Action<string,Action> test)
    {
        test("subtle corners retain dimensions, convexity and consistent selected border segments",()=>
        {
            foreach(var size in new[]{(18f,18f),(22f,22f),(32f,32f),(150f,22f),(320f,60f),(360f,128f)})
            {
                var outline=RoundedGeometry.Outline(size.Item1,size.Item2,RoundedGeometry.ButtonRadius);
                Program.Assert(outline.Length==20 && Area(outline)>0 && Area(outline)<size.Item1*size.Item2);
                foreach(var p in outline)Program.Assert(Math.Abs(p.X)<=size.Item1/2+.001f && Math.Abs(p.Y)<=size.Item2/2+.001f);
                Program.Assert(!Inside(outline,new UiPoint(size.Item1/2,size.Item2/2)));
                var inner=RoundedGeometry.Outline(size.Item1-2,size.Item2-2,RoundedGeometry.ButtonRadius-1);
                Program.Assert(inner.Length==outline.Length && inner.All(p=>Inside(outline,p)));
            }
        });
        test("corner radius bounds handle tiny, zero and invalid image dimensions safely",()=>
        {
            Program.Assert(RoundedGeometry.Radius(2,10,4)==1 && RoundedGeometry.Radius(20,20,-1)==0);
            Program.Assert(RoundedGeometry.Radius(float.NaN,10,4)==0);
            Program.Assert(RoundedGeometry.Outline(0,22,2).Length==0 && RoundedGeometry.Outline(float.PositiveInfinity,22,2).Length==0);
            Program.Assert(Math.Abs(Area(RoundedGeometry.Outline(22,22,0))-484)<.001f);
        });
        test("native rectangle triangles clip to the contour while retaining interpolated UV and tint",()=>
        {
            foreach(var size in new[]{(22f,22f),(140f,22f),(320f,60f)})
            {
                float w=size.Item1,h=size.Item2;var shape=RoundedGeometry.Outline(w,h,2.5f);
                Vertex V(float x,float y)=>new Vertex(x,y,(x+w/2)/w,(y+h/2)/h,((x+w/2)/w+(y+h/2)/h)/2);
                var corners=new[]{V(-w/2,-h/2),V(w/2,-h/2),V(w/2,h/2),V(-w/2,h/2)};
                float area=0;var a=new List<Vertex>();var b=new List<Vertex>();
                foreach(var indices in new[]{new[]{0,1,2},new[]{0,2,3}})
                {
                    var result=RoundedGeometry.ClipTriangle(corners[indices[0]],corners[indices[1]],corners[indices[2]],shape,a,b,v=>new UiPoint(v.X,v.Y),Mix);
                    Program.Assert(result.Count<=shape.Length+3 && result.Count>=3);
                    area+=Area(result.Select(v=>new UiPoint(v.X,v.Y)));
                    foreach(var v in result)
                    {
                        Program.Assert(Inside(shape,new UiPoint(v.X,v.Y)));
                        Program.Assert(Math.Abs(v.U-(v.X+w/2)/w)<.0001f && Math.Abs(v.V-(v.Y+h/2)/h)<.0001f && Math.Abs(v.Tint-(v.U+v.V)/2)<.0001f);
                    }
                }
                Program.Assert(Math.Abs(area-Area(shape))<.02f,"Clipping leaves gaps or overlapping area");
            }
        });
        test("clockwise native UI triangles retain their winding and complete rounded coverage",()=>
        {
            var shape=RoundedGeometry.Outline(22,22,2.5f);var a=new List<Vertex>();var b=new List<Vertex>();
            Vertex V(float x,float y)=>new Vertex(x,y,0,0,1);
            var one=RoundedGeometry.ClipTriangle(V(-11,-11),V(11,11),V(11,-11),shape,a,b,v=>new UiPoint(v.X,v.Y),Mix);
            float area=Area(one.Select(v=>new UiPoint(v.X,v.Y)));
            var two=RoundedGeometry.ClipTriangle(V(-11,-11),V(-11,11),V(11,11),shape,a,b,v=>new UiPoint(v.X,v.Y),Mix);
            area+=Area(two.Select(v=>new UiPoint(v.X,v.Y)));
            Program.Assert(area<0 && Math.Abs(area+Area(shape))<.001f);
        });
        test("interior vertices remain exact and outside triangles disappear without stale buffer contents",()=>
        {
            var shape=RoundedGeometry.Outline(22,22,2.5f);var a=new List<Vertex>();var b=new List<Vertex>();
            var v0=new Vertex(-2,-2,.1f,.2f,.3f);var v1=new Vertex(2,-2,.4f,.5f,.6f);var v2=new Vertex(0,2,.7f,.8f,.9f);
            var result=RoundedGeometry.ClipTriangle(v0,v1,v2,shape,a,b,v=>new UiPoint(v.X,v.Y),(x,y,t)=>throw new Exception("Interior vertex interpolated"));
            Program.Assert(result.Count==3 && result[0].U==v0.U && result[1].V==v1.V && result[2].Tint==v2.Tint);
            for(int n=0;n<20;n++)
            {
                result=RoundedGeometry.ClipTriangle(new Vertex(50,50,1,1,1),new Vertex(60,50,1,1,1),new Vertex(50,60,1,1,1),shape,a,b,v=>new UiPoint(v.X,v.Y),Mix);
                Program.Assert(result.Count==0);
            }
        });
        test("tooltip opacity is modestly higher and its rounding stays separate from controls",()=>
        {
            Program.Assert(RoundedGeometry.TooltipOpacity==.65f && RoundedGeometry.TooltipOpacity>.5f && RoundedGeometry.TooltipOpacity<.8f);
            Program.Assert(RoundedGeometry.TooltipRadius==4 && RoundedGeometry.ButtonRadius==2.5f);
        });
        test("stat markup contains one bounded colour tag per label/value",()=>
        {
            Program.Assert(TooltipMarkup.Colour("Health 34","FF6659")=="<color=#FF6659>Health 34</color>");
            Program.Assert(TooltipMarkup.Colour("Stamina 0","FFD94D").EndsWith("Stamina 0</color>"));
            foreach(var rgb in new[]{"red","#FFFFFF","FFZZ00","FFFFFF>"})
            {bool rejected=false;try{TooltipMarkup.Colour("Eitr 17",rgb);}catch(ArgumentException){rejected=true;}Program.Assert(rejected);}
        });
        test("Mead labels colour healing, stamina and eitr independently with neutral utility/resistance",()=>
        {
            string labels=TooltipMarkup.MeadLabels((MeadFlags)31,"FF6659","FFD94D","D98CFF");
            Program.Assert(labels=="<color=#FF6659>Healing</color>, <color=#FFD94D>Stamina</color>, <color=#D98CFF>Eitr</color>, Resistance, Other");
            Program.Assert(TooltipMarkup.MeadLabels(MeadFlags.Resistance,"FF6659","FFD94D","D98CFF")=="Resistance");
            Program.Assert(TooltipMarkup.MeadLabels(MeadFlags.None,"FF6659","FFD94D","D98CFF")=="Other");
        });
    }
}
