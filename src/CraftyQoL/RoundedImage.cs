using System;
using System.Collections.Generic;
using CraftyQoL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Mesh clipping preserves the native sprite, sliced UVs, material and selectable tint.
    public sealed class RoundedImage : BaseMeshEffect
    {
        internal float Radius=RoundedGeometry.ButtonRadius;
        private Rect _rect;
        private float _radius=-1;
        private UiPoint[] _outline=Array.Empty<UiPoint>();
        // uGUI rebuilds on the main thread. Share scratch storage; keep only each image's contour.
        private static readonly List<UIVertex> Stream=new List<UIVertex>(),WorkA=new List<UIVertex>(),WorkB=new List<UIVertex>();
        private static bool _busy;
        private static readonly Func<UIVertex,UiPoint> Position=v=>new UiPoint(v.position.x,v.position.y);
        private static readonly Func<UIVertex,UIVertex,float,UIVertex> Blend=Interpolate;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(_busy || !IsActive() || !(graphic is Image image))return;
            Rect rect=image.GetPixelAdjustedRect();
            if(rect!=_rect || Radius!=_radius)
            {
                _rect=rect;_radius=Radius;_outline=RoundedGeometry.Outline(rect.width,rect.height,Radius);
                for(int i=0;i<_outline.Length;i++)_outline[i]=new UiPoint(_outline[i].X+rect.center.x,_outline[i].Y+rect.center.y);
            }
            if(_outline.Length==0)return;
            _busy=true;
            try
            {
                Stream.Clear();mesh.GetUIVertexStream(Stream);mesh.Clear();
                for(int at=0;at+2<Stream.Count;at+=3)
                {
                    var polygon=RoundedGeometry.ClipTriangle(Stream[at],Stream[at+1],Stream[at+2],_outline,WorkA,WorkB,Position,Blend);
                    int start=mesh.currentVertCount;foreach(var vertex in polygon)mesh.AddVert(vertex);
                    for(int i=2;i<polygon.Count;i++)mesh.AddTriangle(start,start+i-1,start+i);
                }
            }
            finally{_busy=false;}
        }
        private static UIVertex Interpolate(UIVertex a,UIVertex b,float t)
        {
            a.position=Vector3.LerpUnclamped(a.position,b.position,t);a.color=Color32.Lerp(a.color,b.color,t);
            a.uv0=Vector4.LerpUnclamped(a.uv0,b.uv0,t);a.uv1=Vector4.LerpUnclamped(a.uv1,b.uv1,t);
            a.uv2=Vector4.LerpUnclamped(a.uv2,b.uv2,t);a.uv3=Vector4.LerpUnclamped(a.uv3,b.uv3,t);
            a.normal=Vector3.LerpUnclamped(a.normal,b.normal,t);a.tangent=Vector4.LerpUnclamped(a.tangent,b.tangent,t);return a;
        }
    }
}
