using System;
using CraftyQoL.Core;
using HarmonyLib;
using UnityEngine;

namespace CraftyQoL
{
    internal static class InputScope
    {
        [ThreadStatic] internal static FocusMode Mode;
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class GuiInputScopePatch
    {
        internal struct ScopeState { internal bool Entered; internal FocusMode Previous; }
        private static void Prefix(InventoryGui __instance, out ScopeState __state)
        {
            __state = new ScopeState { Entered = true, Previous = InputScope.Mode };
            StorageSorting.Current?.InventoryInput(__instance);
            RecipeTracker.Current?.InventoryInput(__instance);
            FoodView? view = FoodView.Current;
            InputScope.Mode = StorageSorting.Released || RecipeTracker.Released ? FocusMode.Released : StorageSorting.Focus!=FocusMode.None?StorageSorting.Focus: RecipeTracker.Focus != FocusMode.None ? RecipeTracker.Focus :
                view && view!.IsFor(__instance) ? view.Focus : FocusMode.None;
        }
        private static void Postfix(ScopeState __state) { if (__state.Entered) InputScope.Mode = __state.Previous; }
        private static Exception? Finalizer(Exception? __exception, ScopeState __state)
        { if (__state.Entered) InputScope.Mode = __state.Previous; return __exception; }
    }

    [HarmonyPatch(typeof(ZInput), "GetButtonDown", new[] { typeof(string) })]
    internal static class ScopedButtonPatch
    {
        private static bool Prefix(string __0, ref bool __result)
        {
            if (!InputPolicy.BlockAction(__0, InputScope.Mode)) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(ZInput), "GetKeyDown", new[] { typeof(KeyCode), typeof(bool) })]
    internal static class ScopedEscapePatch
    {
        private static bool Prefix(KeyCode __0, ref bool __result)
        {
            if (InputScope.Mode == FocusMode.None || __0 != KeyCode.Escape) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(UIGamePad), "ButtonPressed")]
    internal static class NativeShortcutPatch
    {
        private static bool Prefix(UIGamePad __instance, ref bool __result)
        {
            if ((RecipeTracker.Focus != FocusMode.None || RecipeTracker.Released || StorageSorting.Focus!=FocusMode.None || StorageSorting.Released) && InventoryGui.instance && __instance.transform.IsChildOf(InventoryGui.instance.transform))
            { __result = false; return false; }
            FoodView? view = FoodView.Current;
            if (!view || view!.Focus == FocusMode.None || !__instance.transform.IsChildOf(view.transform)) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(PlayerController), "TakeInput", new[] { typeof(bool) })]
    internal static class SearchCameraPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (RecipeTracker.BlocksGameInput || StorageSorting.BlocksGameInput) { __result = false; return false; }
            FoodView? view = FoodView.Current;
            if (!view || (view!.Focus & FocusMode.Text) == 0) return true;
            __result = false; return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Hide")]
    internal static class HidePatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            Plugin.Instance?.InventoryClosed();
            if (FoodView.Current && FoodView.Current!.IsFor(__instance)) FoodView.Current.BeginClose();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
    internal static class DestroyPatch
    {
        private static void Prefix(InventoryGui __instance)
        { if (FoodView.Current && FoodView.Current!.gameObject == __instance.gameObject) FoodView.Current.CloseOwned(); }
    }
}
