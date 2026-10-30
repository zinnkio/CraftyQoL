using System;
using System.Globalization;
using System.Text;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    internal sealed class RowDecoration : IDisposable
    {
        private readonly RowWidgetPool _pool;
        private readonly RowWidgets _widgets;
        internal Button Favourite=>_widgets.Favourite;
        internal readonly string Key;
        private readonly bool _persistable;
        private readonly RectTransform _icon, _nameRect;
        private readonly TMP_Text _name;
        private readonly Vector2 _iconSize, _nameSize, _writtenNameSize;
        private readonly string _originalName, _writtenName;
        private readonly UITooltip _tooltip=null!;
        private readonly bool _addedTooltip;
        private readonly string _oldTooltip="";
        private string _writtenTooltip="";
        private bool _released;

        internal RowDecoration(InventoryGui gui,RowWidgetPool pool,RecipeRow row,bool favourite,Action toggle,bool equipment=false,bool mead=false,bool generic=false)
        {
            _pool=pool;Key=row.Info.Key;_persistable=row.Info.Persistable;
            _icon=row.Element.transform.Find("icon") as RectTransform??throw new NotSupportedException("Native row icon unavailable.");
            _name=row.Element.transform.Find("name").GetComponent<TMP_Text>();_nameRect=_name.rectTransform;
            _iconSize=_icon.sizeDelta;_nameSize=_nameRect.sizeDelta;
            _originalName=_name.text;_writtenName=_originalName;
            _writtenNameSize=_nameSize-new Vector2(ToolbarGeometry.SmallButton+ToolbarGeometry.Gap,0);
            _widgets=pool.Rent();
            try
            {
                FoodFlags flags=equipment || mead?FoodFlags.None:row.Info.Food.Categories;
                _widgets.Bind(row.Element.transform,_name.font,flags,toggle,_persistable);
                if(flags!=FoodFlags.None)_icon.sizeDelta=new Vector2(22,22);
                _nameRect.sizeDelta=_writtenNameSize;_name.text=_writtenName;
                SetFavourite(favourite,false);
                UITooltip? existing=row.Element.GetComponent<UITooltip>();
                _addedTooltip=existing==null;_tooltip=UiStyle.Tooltip(gui,row.Element);_oldTooltip=_tooltip.m_text;
                _writtenTooltip=Tooltip(row.Info,equipment,mead,generic);_tooltip.m_text=_writtenTooltip;
                _widgets.Show();
            }
            catch{Dispose();throw;}
        }
        internal void SetFavourite(bool favourite,bool saveFailed)
        {if(!_released)_widgets.SetFavourite(favourite,_persistable,saveFailed);}
        private static string Tooltip(RecipeInfo info, bool equipment, bool mead,bool generic=false)
        {
            var text = new StringBuilder(info.Name);
            if(generic)text.Append("\nOther recipe");
            else if (equipment) text.Append("\nCategories: ").Append(info.Equipment);
            else if (mead) return text.Append("\nPotion categories (after fermentation where applicable): ").Append(UiStyle.MeadLabels(info.Mead)).ToString();
            else if (info.Nutrition == NutritionSource.Unknown || info.Nutrition == NutritionSource.Ambiguous)
                text.Append("\nNutrition unavailable");
            else
            {
                text.Append(info.Nutrition == NutritionSource.Feast ? "\nPer serving: " : info.Nutrition == NutritionSource.Cooked ? "\nAfter cooking: " : "\nNutrition: ");
                text.Append(UiStyle.StatText("Health "+Number(info.Food.Health),UiStyle.Health)).Append(" / ").Append(UiStyle.StatText("Stamina "+Number(info.Food.Stamina),UiStyle.Stamina)).Append(" / ").Append(UiStyle.StatText("Eitr "+Number(info.Food.Eitr),UiStyle.Eitr));
            }
            return text.ToString();
        }
        private static string Number(double? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.CurrentCulture) : "?";
        public void Dispose()
        {
            if(_released)return;_released=true;
            try
            {
                if(_icon && _icon.sizeDelta==new Vector2(22,22))_icon.sizeDelta=_iconSize;
                if(_nameRect && _nameRect.sizeDelta==_writtenNameSize)_nameRect.sizeDelta=_nameSize;
                if(_name && _name.text==_writtenName)_name.text=_originalName;
                if(_tooltip && _tooltip.m_text==_writtenTooltip)
                {
                    if(_addedTooltip){_tooltip.enabled=false;UnityEngine.Object.Destroy(_tooltip);}
                    else _tooltip.m_text=_oldTooltip;
                }
            }
            finally{_pool.Return(_widgets);}
        }
    }
}
