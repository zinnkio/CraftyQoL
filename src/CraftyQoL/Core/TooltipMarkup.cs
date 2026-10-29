using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal static class TooltipMarkup
    {
        internal static string Colour(string text,string rgb)
        {
            if(rgb.Length!=6)throw new ArgumentException("Six hexadecimal RGB digits required.");
            foreach(char c in rgb)if(!(c>='0' && c<='9' || c>='a' && c<='f' || c>='A' && c<='F'))throw new ArgumentException("Invalid RGB colour.");
            return "<color=#"+rgb+">"+text+"</color>";
        }
        internal static string MeadLabels(MeadFlags flags,string health,string stamina,string eitr)
        {
            var words=new List<string>();
            if((flags&MeadFlags.Healing)!=0)words.Add(Colour("Healing",health));
            if((flags&MeadFlags.Stamina)!=0)words.Add(Colour("Stamina",stamina));
            if((flags&MeadFlags.Eitr)!=0)words.Add(Colour("Eitr",eitr));
            if((flags&MeadFlags.Resistance)!=0)words.Add("Resistance");
            if((flags&MeadFlags.Other)!=0 || words.Count==0)words.Add("Other");
            return string.Join(", ",words);
        }
    }
}
