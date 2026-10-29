using CraftyQoL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    public sealed class SelectionBorderGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Rect rect=GetPixelAdjustedRect();
            float thickness=Mathf.Min(ToolbarGeometry.Border,Mathf.Min(rect.width,rect.height)/2);
            if(thickness<=0)return;
            var outer=RoundedGeometry.Outline(rect.width,rect.height,RoundedGeometry.ButtonRadius);
            var inner=RoundedGeometry.Outline(rect.width-2*thickness,rect.height-2*thickness,Mathf.Max(0,RoundedGeometry.ButtonRadius-thickness));
            if(inner.Length!=outer.Length)return;
            for(int i=0;i<outer.Length;i++)
            {
                mesh.AddVert(new Vector3(rect.center.x+outer[i].X,rect.center.y+outer[i].Y),color,Vector2.zero);
                mesh.AddVert(new Vector3(rect.center.x+inner[i].X,rect.center.y+inner[i].Y),color,Vector2.zero);
            }
            for(int i=0;i<outer.Length;i++)
            {int next=(i+1)%outer.Length;mesh.AddTriangle(i*2,next*2,next*2+1);mesh.AddTriangle(i*2,next*2+1,i*2+1);}
        }
    }
}
