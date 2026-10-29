using System;

namespace CraftyQoL.Core
{
    internal readonly struct StarPoint
    {
        internal readonly float X, Y;
        internal StarPoint(float x, float y) { X = x; Y = y; }
    }
    internal static class StarGeometry
    {
        // Original ten-vertex star. Unit radius; no bitmap or font dependency.
        internal static readonly StarPoint[] Points = Create();
        private static StarPoint[] Create()
        {
            var points = new StarPoint[10];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = Math.PI * 0.5 + i * Math.PI / 5;
                double radius = i % 2 == 0 ? 1 : 0.45;
                points[i] = new StarPoint((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
            }
            return points;
        }
    }
}
