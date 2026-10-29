using CraftyQoL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    public sealed class StarGraphic : MaskableGraphic
    {
        private bool _filled;
        internal void SetFilled(bool filled)
        {
            if (_filled == filled) return;
            _filled = filled; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Max(0, Mathf.Min(rect.width, rect.height) * 0.45f);
            void Vertex(float x, float y) => mesh.AddVert(new Vector3(center.x + x * radius, center.y + y * radius), color, Vector2.zero);
            if (_filled)
            {
                Vertex(0, 0);
                foreach (StarPoint point in StarGeometry.Points) Vertex(point.X, point.Y);
                for (int i = 0; i < 10; i++) mesh.AddTriangle(0, (i + 1) % 10 + 1, i + 1);
            }
            else
            {
                foreach (StarPoint point in StarGeometry.Points) { Vertex(point.X, point.Y); Vertex(point.X * 0.72f, point.Y * 0.72f); }
                for (int i = 0; i < 10; i++)
                {
                    int a = i * 2, b = (i + 1) % 10 * 2;
                    mesh.AddTriangle(a, b + 1, b); mesh.AddTriangle(a, a + 1, b + 1);
                }
            }
        }
    }
}
