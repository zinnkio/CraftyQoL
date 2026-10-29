using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    internal static class MeadCategoryIcons
    {
        internal static void Attach(Button[] buttons)
        {
            // Verified native final-potion templates, independent of recipe availability.
            string[] prefabs={"","MeadHealthMinor","MeadStaminaMinor","MeadEitrMinor","MeadPoisonResist","MeadStrength"};
            for(int i=0;i<buttons.Length;i++)
            {
                if(i==0)
                {
                    var grid=new GameObject("CraftyQoL.AllPotions",typeof(RectTransform),typeof(EquipmentGridGraphic));
                    EquipmentCategoryIcons.Center((RectTransform)grid.transform,buttons[i].transform,18);
                    grid.GetComponent<EquipmentGridGraphic>().raycastTarget=false;
                }
                else
                {
                    Sprite? sprite=EquipmentCategoryIcons.Icon(prefabs[i]);
                    if(!sprite)continue; // Retain short readable category text if a mod removes this template.
                    EquipmentCategoryIcons.Add(buttons[i],sprite!,18,Vector2.zero);
                }
                buttons[i].GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
            }
        }
    }
}
