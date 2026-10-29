using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    internal sealed class RowDecoration : IDisposable
    {
        private readonly GameObject _badges;
        private readonly List<RoundedImage> _rounded=new List<RoundedImage>();
        internal readonly Button Favourite;
        internal readonly string Key;
        private readonly StarGraphic _star;
        private readonly bool _persistable;
        private readonly RectTransform _icon, _nameRect;
        private readonly TMP_Text _name;
        private readonly Vector2 _iconSize, _nameSize, _writtenNameSize;
        private readonly string _originalName, _writtenName;
        private readonly UITooltip _tooltip;
        private readonly bool _addedTooltip;
        private readonly string _oldTooltip;
        private string _writtenTooltip = "";

        internal RowDecoration(InventoryGui gui, RecipeRow row, bool favourite, Action toggle, bool equipment = false, bool mead = false,bool generic=false)
        {
            Key = row.Info.Key; _persistable = row.Info.Persistable;
            _icon = row.Element.transform.Find("icon") as RectTransform ?? throw new NotSupportedException("Native row icon unavailable.");
            _name = row.Element.transform.Find("name").GetComponent<TMP_Text>();
            _nameRect = _name.rectTransform;
            _iconSize = _icon.sizeDelta; _nameSize = _nameRect.sizeDelta;
            _originalName = _name.text;
            _writtenName = _originalName;
            _writtenNameSize = _nameSize - new Vector2(ToolbarGeometry.SmallButton + ToolbarGeometry.Gap, 0);
            _badges = new GameObject("CraftyQoL.Badges", typeof(RectTransform));
            _badges.transform.SetParent(row.Element.transform, false);
            var root = (RectTransform)_badges.transform;
            root.anchorMin = root.anchorMax = new Vector2(0, 0.5f); root.pivot = new Vector2(0, 0.5f);
            root.anchoredPosition = Vector2.zero; root.sizeDelta = new Vector2(36, 32);
            try
            {
                CraftButtonStyle.Attach(row.Element.GetComponent<Button>(),_rounded);
                FoodFlags flags = equipment || mead ? FoodFlags.None : row.Info.Food.Categories;
                int count = ((flags & FoodFlags.Health) != 0 ? 1 : 0) + ((flags & FoodFlags.Stamina) != 0 ? 1 : 0) + ((flags & FoodFlags.Eitr) != 0 ? 1 : 0);
                int index = 0;
                void Badge(FoodFlags flag, string letter, Color colour)
                {
                    if ((flags & flag) == 0) return;
                    var go = new GameObject("CraftyQoL." + letter, typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(root, false);
                    var text = go.GetComponent<TextMeshProUGUI>();
                    text.font = _name.font; text.text = letter; text.fontSize = 11; text.color = colour;
                    text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                    var rect = text.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
                    rect.anchoredPosition = new Vector2(29, (count - 1) * 5.5f - index++ * 11);
                    rect.sizeDelta = new Vector2(12, 11);
                }
                Badge(FoodFlags.Health, "H", UiStyle.Health);
                Badge(FoodFlags.Stamina, "S", UiStyle.Stamina);
                Badge(FoodFlags.Eitr, "E", UiStyle.Eitr);
                if (count > 0) _icon.sizeDelta = new Vector2(22, 22);
                _nameRect.sizeDelta = _writtenNameSize; _name.text = _writtenName;
                Favourite = NativeToolbar.MakeButton(gui, row.Element.transform, "", "Favourite recipe", toggle,
                    0, 0, ToolbarGeometry.SmallButton, ToolbarGeometry.SmallButton);
                var favouriteRect = (RectTransform)Favourite.transform;
                favouriteRect.anchorMin = favouriteRect.anchorMax = favouriteRect.pivot = new Vector2(1, 0.5f);
                favouriteRect.anchoredPosition = Vector2.zero;
                Favourite.interactable = row.Info.Persistable;
                Favourite.navigation = new Navigation { mode = Navigation.Mode.None };
                _star = UiStyle.AddStar(Favourite);
                SetFavourite(favourite, false);
                UITooltip? existing = row.Element.GetComponent<UITooltip>();
                _addedTooltip = existing == null;
                _tooltip = UiStyle.Tooltip(gui, row.Element);
                _oldTooltip = _tooltip.m_text;
                _writtenTooltip = Tooltip(row.Info, equipment, mead,generic);
                _tooltip.m_text = _writtenTooltip;
            }
            catch
            {
                CraftButtonStyle.Restore(_rounded);
                if (_badges) UnityEngine.Object.Destroy(_badges);
                if (Favourite) UnityEngine.Object.Destroy(Favourite.gameObject);
                if (_icon) _icon.sizeDelta = _iconSize;
                if (_nameRect) _nameRect.sizeDelta = _nameSize;
                if (_name) _name.text = _originalName;
                if (_tooltip)
                {
                    if (_addedTooltip) UnityEngine.Object.Destroy(_tooltip);
                    else _tooltip.m_text = _oldTooltip;
                }
                throw;
            }
        }
        internal void SetFavourite(bool favourite, bool saveFailed)
        {
            _star.SetFilled(favourite);
            _star.color = UiStyle.StarColour(favourite);
            string message = !_persistable ? "Recipe identity is not unique; favourite unavailable" :
                (favourite ? "Remove favourite" : "Add favourite") + (saveFailed ? "\nChanges are in memory; saving failed" : "");
            Favourite.GetComponent<UITooltip>().Set("", message);
        }
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
            CraftButtonStyle.Restore(_rounded);
            if (_icon && _icon.sizeDelta == new Vector2(22, 22)) _icon.sizeDelta = _iconSize;
            if (_nameRect && _nameRect.sizeDelta == _writtenNameSize) _nameRect.sizeDelta = _nameSize;
            if (_name && _name.text == _writtenName) _name.text = _originalName;
            if (_tooltip && _tooltip.m_text == _writtenTooltip)
            {
                if (_addedTooltip) UnityEngine.Object.Destroy(_tooltip);
                else _tooltip.m_text = _oldTooltip;
            }
            if (_badges) UnityEngine.Object.Destroy(_badges);
            if (Favourite) UnityEngine.Object.Destroy(Favourite.gameObject);
        }
    }
}
