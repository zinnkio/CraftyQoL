using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal readonly struct UiPoint
    {
        internal readonly float X,Y;
        internal UiPoint(float x,float y){X=x;Y=y;}
    }
    internal static class RoundedGeometry
    {
        internal const float ButtonRadius=2.5f,TooltipRadius=4,TooltipOpacity=.65f;
        private static bool Finite(float n)=>!float.IsNaN(n) && !float.IsInfinity(n);
        internal static float Radius(float width,float height,float radius)=>!Finite(width) || !Finite(height) || !Finite(radius)?0:Math.Max(0,Math.Min(radius,Math.Min(width,height)/2));
        internal static UiPoint[] Outline(float width,float height,float radius)
        {
            if(!Finite(width) || !Finite(height) || width<=0 || height<=0)return Array.Empty<UiPoint>();
            float r=Radius(width,height,radius),x=width/2-r,y=height/2-r;
            var points=new UiPoint[20];
            for(int corner=0;corner<4;corner++)
                for(int step=0;step<=4;step++)
                {
                    double a=(-90+corner*90+step*22.5)*Math.PI/180;
                    float cx=corner<2?x:-x,cy=corner==0 || corner==3?-y:y;
                    points[corner*5+step]=new UiPoint(cx+r*(float)Math.Cos(a),cy+r*(float)Math.Sin(a));
                }
            return points;
        }
        private static float Side(UiPoint a,UiPoint b,UiPoint p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
        // Clip native image triangles, interpolating all vertex attributes through the caller.
        internal static List<T> ClipTriangle<T>(T a,T b,T c,UiPoint[] outline,List<T> workA,List<T> workB,Func<T,UiPoint> point,Func<T,T,float,T> interpolate)
        {
            workA.Clear();workA.Add(a);workA.Add(b);workA.Add(c);var source=workA;var target=workB;
            for(int edge=0;edge<outline.Length && source.Count>0;edge++)
            {
                target.Clear();UiPoint p=outline[edge],q=outline[(edge+1)%outline.Length];
                T previous=source[source.Count-1];float before=Side(p,q,point(previous));
                foreach(T current in source)
                {
                    float after=Side(p,q,point(current));bool wasInside=before>=0,isInside=after>=0;
                    if(wasInside!=isInside)target.Add(interpolate(previous,current,Math.Max(0,Math.Min(1,before/(before-after)))));
                    if(isInside)target.Add(current);
                    previous=current;before=after;
                }
                var swap=source;source=target;target=swap;
            }
            return source;
        }
    }
}
