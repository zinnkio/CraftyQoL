using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Own only our reversible effects on the five native Craft controls. Upgrade/unrelated scope restores them.
    internal sealed class CraftButtonStyle : IDisposable
    {
        private readonly List<RoundedImage> _owned=new List<RoundedImage>();
        internal CraftButtonStyle(InventoryGui gui)
        {
            foreach(Button button in new[]{gui.m_tabCraft,gui.m_tabUpgrade,gui.m_craftButton,gui.m_repairButton,gui.m_variantButton})
                Attach(button,_owned);
            Add(gui.m_repairButtonGlow,_owned);
        }
        internal static void Attach(Button button,List<RoundedImage> owned)
        {
            if(!button)return;Add(button.GetComponent<Image>(),owned);
            foreach(string name in new[]{"Selected","selected"})
            {Transform selected=button.transform.Find(name);if(selected)Add(selected.GetComponent<Image>(),owned);}
        }
        private static void Add(Image image,List<RoundedImage> owned)
        {if(!image)return;var effect=UiStyle.Round(image,Core.RoundedGeometry.ButtonRadius,out bool added);if(added)owned.Add(effect);}
        internal static void Restore(List<RoundedImage> owned)
        {foreach(var effect in owned)if(effect){effect.enabled=false;UnityEngine.Object.Destroy(effect);}owned.Clear();}
        public void Dispose()=>Restore(_owned);
    }
}
