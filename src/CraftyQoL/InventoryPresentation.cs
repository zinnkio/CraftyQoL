using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace CraftyQoL
{
    // Observe the native inventory animation; never drive its state or its input binding.
    internal static class InventoryPresentation
    {
        private static InventoryGui? _owner;
        private static Animator? _animator;
        private static bool _qualified;
        private static readonly int Hidden = Animator.StringToHash("Base Layer.inventory_hidden");
        private static readonly FieldInfo? Drag = AccessTools.DeclaredField(typeof(InventoryGui), "m_dragGo");
        private static Animator? Resolve(InventoryGui gui)
        {
            if (_owner != gui || !_animator)
            {
                _owner = gui;
                _animator = gui ? gui.GetComponent<Animator>() : null;
                _qualified = _animator && _animator!.runtimeAnimatorController && _animator.HasState(0, Hidden);
            }
            return _animator;
        }
        internal static bool Open
        {
            get
            {
                var animator = Resolve(InventoryGui.instance);
                return animator && animator!.isActiveAndEnabled && animator.GetBool("visible");
            }
        }
        internal static bool CanAnimateClose(InventoryGui gui)
        { var animator = Resolve(gui); return _qualified && animator && animator!.isActiveAndEnabled; }
        internal static bool FinishedClosing(InventoryGui gui)
        {
            var animator = Resolve(gui);
            return !_qualified || !animator || !animator!.isActiveAndEnabled ||
                (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Hidden);
        }
        internal static bool CanInteract
        {
            get
            {
                var gui = InventoryGui.instance;
                // Fail closed if the native item-drag binding changes; don't capture a resource drop.
                return Open && gui && Drag != null && Drag.FieldType == typeof(GameObject) &&
                    !(Drag.GetValue(gui) as GameObject) && !InventoryGui.WaitForStack() &&
                    (!gui.m_splitDialog || !gui.m_splitDialog.IsActive) &&
                    (!gui.m_variantDialog || !gui.m_variantDialog.gameObject.activeSelf) &&
                    (!gui.m_skillsDialog || !gui.m_skillsDialog.gameObject.activeSelf) &&
                    (!gui.m_textsDialog || !gui.m_textsDialog.gameObject.activeSelf) &&
                    (!gui.m_trophiesPanel || !gui.m_trophiesPanel.activeSelf) &&
                    (!gui.m_achievementsPanel || !gui.m_achievementsPanel.gameObject.activeSelf);
            }
        }
    }
}
