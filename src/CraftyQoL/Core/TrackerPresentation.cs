using System;
using System.Globalization;

namespace CraftyQoL.Core
{
    internal readonly struct TrackerHudGeometry
    {
        internal const float RowHeight=24;
        internal readonly TrackerLayout Frame;
        internal readonly float ViewportY,ViewportHeight;
        internal readonly bool Scrolling;
        internal TrackerHudGeometry(float width,float height,int rows,bool editing,bool settings,bool minimised,bool warning)
        {
            ViewportY=52+(editing && settings?24:0)+(warning?16:0);
            float maxHeight=new TrackerLayout(width,height).Height;
            float content=Math.Max(1,rows)*RowHeight;
            Scrolling=!minimised && ViewportY+6+content>maxHeight;
            float footer=editing && Scrolling?26:0;
            Frame=new TrackerLayout(width,height,minimised?26:ViewportY+6+content+footer);
            ViewportHeight=minimised?0:Math.Max(0,Frame.Height-ViewportY-6-footer);
        }
    }
    internal static class TrackerText
    {
        internal static string Quantity(long owned,long required)=>Math.Max(0,owned).ToString(CultureInfo.InvariantCulture)+"/"+Math.Max(0,required).ToString(CultureInfo.InvariantCulture);
    }
}
