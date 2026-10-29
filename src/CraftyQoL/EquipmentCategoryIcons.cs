using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    internal static class EquipmentCategoryIcons
    {
        internal static void Attach(Button[] buttons)
        {
            // Resolve through native registered item metadata, never names of available/locked recipes.
            // These are generic category pictures; no item names or tooltips are exposed.
            string[] items = { "", "SwordBronze", "ArmorBronzeChest", "ShieldWood", "Hammer", "ArrowWood", "StaffFireball", "Coins" };
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (i == (int)EquipmentCategory.All)
                {
                    var grid = new GameObject("CraftyQoL.CategoryGrid", typeof(RectTransform), typeof(EquipmentGridGraphic));
                    Center((RectTransform)grid.transform, button.transform, EquipmentToolbarGeometry.IconSize);
                    grid.GetComponent<EquipmentGridGraphic>().raycastTarget = false;
                }
                else
                {
                    Sprite? sprite = Icon(items[i]);
                    Sprite? second = i == (int)EquipmentCategory.Other ? Icon("Resin") : null;
                    if (!sprite || (i == (int)EquipmentCategory.Other && !second)) continue; // Keep qualified compact text if a mod removes a template.
                    if (second)
                    {
                        Add(button, sprite!, 12, new Vector2(-3, 3));
                        Add(button, second!, 12, new Vector2(3, -3));
                    }
                    else Add(button, sprite!, EquipmentToolbarGeometry.IconSize, Vector2.zero);
                }
                button.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
            }
        }
        internal static Sprite? Icon(string prefab)
        {
            GameObject item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null!;
            ItemDrop drop = item ? item.GetComponent<ItemDrop>() : null!;
            return drop && drop.m_itemData?.m_shared != null ? drop.m_itemData.GetIcon() : null;
        }
        internal static void Add(Button button, Sprite sprite, float size, Vector2 offset)
        {
            var icon = new GameObject("CraftyQoL.CategoryIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)icon.transform;
            Center(rect, button.transform, size); rect.anchoredPosition = offset;
            var image = icon.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        }
        internal static void Center(RectTransform rect, Transform parent, float size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(size, size);
        }
    }
    // Four simple quads avoid a texture asset, a font glyph or a global loaded-resource scan for All.
    public sealed class EquipmentGridGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float side = Mathf.Min(rect.width, rect.height), cell = side / 3f, gap = side / 9f;
            Vector2 start = rect.center - new Vector2(cell + gap / 2, cell + gap / 2);
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    int index = mesh.currentVertCount;
                    float left = start.x + x * (cell + gap), bottom = start.y + y * (cell + gap);
                    mesh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
                    mesh.AddVert(new Vector3(left, bottom + cell), color, Vector2.zero);
                    mesh.AddVert(new Vector3(left + cell, bottom + cell), color, Vector2.zero);
                    mesh.AddVert(new Vector3(left + cell, bottom), color, Vector2.zero);
                    mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
                }
        }
    }
}
