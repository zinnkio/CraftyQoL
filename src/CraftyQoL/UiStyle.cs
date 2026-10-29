using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using CraftyQoL.Core;

namespace CraftyQoL
{
    internal static class UiStyle
    {
        internal const float TextSize = 11, MinimumTextSize = 10, SearchSize = 12, PlaceholderSize = 11, CaptionSize = 9, StarSize = 16;
        internal const float TextPaddingX = 2, TextPaddingY = 1, SearchPaddingX = 5, SearchPaddingY = 2;
        internal static readonly Color Surface = new Color(0.62f, 0.57f, 0.49f);
        internal static readonly Color InputSurface = new Color(0.74f, 0.68f, 0.57f);
        internal static readonly Color Accent = new Color(0.84f, 0.69f, 0.44f);
        internal static readonly Color DisabledSurface = new Color(0.34f, 0.32f, 0.29f);
        internal static readonly Color GoldStar = new Color(1, 0.8f, 0.3f);
        internal static readonly Color OutlineStar = new Color(0.82f, 0.8f, 0.73f);
        private static GameObject? _tooltipPrefab;
        internal static UITooltip Tooltip(InventoryGui gui, GameObject target)
        {
            UITooltip tip = target.GetComponent<UITooltip>() ?? target.AddComponent<UITooltip>();
            if (!tip.m_tooltipPrefab)
            {
                if (!_tooltipPrefab)
                    foreach (UITooltip native in gui.GetComponentsInChildren<UITooltip>(true))
                        if (native.m_tooltipPrefab) { _tooltipPrefab = native.m_tooltipPrefab; break; }
                if (!_tooltipPrefab) throw new NotSupportedException("Native tooltip template unavailable.");
                tip.m_tooltipPrefab = _tooltipPrefab;
            }
            return tip;
        }
        internal static StarGraphic AddStar(Button button)
        {
            button.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
            var go = new GameObject("CraftyQoL.Star", typeof(RectTransform), typeof(StarGraphic));
            go.transform.SetParent(button.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(StarSize, StarSize);
            var star = go.GetComponent<StarGraphic>(); star.raycastTarget = false;
            return star;
        }
        internal static readonly Color Health = new Color(1, 0.4f, 0.35f);
        internal static readonly Color Stamina = new Color(1, 0.85f, 0.3f);
        internal static readonly Color Eitr = new Color(0.85f, 0.55f, 1);
        internal static readonly Color Neutral = new Color(0.40f, 0.39f, 0.36f);
        internal static Color FoodColour(int index) => index == 1 ? Health : index == 2 ? Stamina : index == 3 ? Eitr : Color.white;
        internal static Color StarColour(bool favourite) => favourite ? GoldStar : OutlineStar;
        internal static Color Swatch(Color biome) => Color.Lerp(Neutral, biome, 0.8f);
        internal static ColorBlock Colours(Color surface)
        {
            ColorBlock colours = ColorBlock.defaultColorBlock;
            colours.normalColor = surface;
            colours.highlightedColor = colours.selectedColor = Color.Lerp(surface, Color.white, 0.18f);
            colours.pressedColor = Color.Lerp(surface, Color.black, 0.24f);
            colours.disabledColor = DisabledSurface; colours.fadeDuration = 0.08f;
            return colours;
        }
        internal static void NativeBorder(Image image)
        {
            if (!image || !image.sprite) return;
            Vector4 border = image.sprite.border;
            float largest = Mathf.Max(Mathf.Max(border.x, border.y), Mathf.Max(border.z, border.w));
            if (largest <= 0) return;
            image.type = Image.Type.Sliced; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = Mathf.Max(1, largest / Mathf.Max(0.01f, image.pixelsPerUnit) / ToolbarGeometry.Border);
        }
        internal static RoundedImage Round(Image image,float radius,out bool added)
        {
            foreach(var effect in image.GetComponents<RoundedImage>())if(effect.enabled){added=false;return effect;}
            var rounded=image.gameObject.AddComponent<RoundedImage>();rounded.Radius=radius;added=true;return rounded;
        }
        internal static void Round(Image image)=>Round(image,RoundedGeometry.ButtonRadius,out _);
        internal static string StatText(string value,Color colour)=>TooltipMarkup.Colour(value,ColorUtility.ToHtmlStringRGB(colour));
        internal static string FoodLabel(int option,string label)=>option==0?label:StatText(label,FoodColour(option));
        internal static string MeadLabels(MeadFlags flags)=>TooltipMarkup.MeadLabels(flags,ColorUtility.ToHtmlStringRGB(Health),ColorUtility.ToHtmlStringRGB(Stamina),ColorUtility.ToHtmlStringRGB(Eitr));
        internal static void Selected(Button button, bool selected, Color colour, Color? surface = null)
        {
            button.transform.Find("CraftyQoL.Selected").gameObject.SetActive(selected && button.interactable);
            Color background = surface ?? Surface;
            button.colors = Colours(selected ? Color.Lerp(background, Accent, 0.28f) : background);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            text.color = button.interactable ? colour : new Color(0.6f, 0.58f, 0.53f);
            text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
        internal static void AddSelectionMarker(Button button)
        {
            var go = new GameObject("CraftyQoL.Selected", typeof(RectTransform), typeof(SelectionBorderGraphic));
            go.transform.SetParent(button.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var border = go.GetComponent<SelectionBorderGraphic>(); border.color = Accent; border.raycastTarget = false;
            go.SetActive(false);
        }
        internal static RectTransform Separator(Transform parent, UiBox box)
        {
            var go = new GameObject("CraftyQoL.Separator", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(box.X, -box.Y); rect.sizeDelta = new Vector2(box.Width, box.Height);
            var image = go.GetComponent<Image>(); image.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f); image.raycastTarget = false;
            return rect;
        }
    }
}
