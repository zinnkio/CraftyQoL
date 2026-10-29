using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Original pushpin silhouette: no font glyph, texture or redistributed asset.
    public sealed class PinGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Rect rect=GetPixelAdjustedRect();float scale=Mathf.Min(rect.width,rect.height)/22f;
            void Polygon(params Vector2[] points)
            {
                int start=mesh.currentVertCount;
                foreach(Vector2 p in points)
                {
                    float x=p.x*0.8660254f-p.y*0.5f,y=p.x*0.5f+p.y*0.8660254f;
                    mesh.AddVert(new Vector3(rect.center.x+x*scale,rect.center.y+y*scale),color,Vector2.zero);
                }
                for(int i=1;i<points.Length-1;i++)mesh.AddTriangle(start,start+i+1,start+i);
            }
            Polygon(new Vector2(-6,5),new Vector2(6,5),new Vector2(6,8),new Vector2(-6,8));
            Polygon(new Vector2(-3,5),new Vector2(3,5),new Vector2(2,-1),new Vector2(-2,-1));
            Polygon(new Vector2(-5,-1),new Vector2(5,-1),new Vector2(5,-3),new Vector2(-5,-3));
            Polygon(new Vector2(-1,-3),new Vector2(1,-3),new Vector2(0,-10));
        }
    }
}
