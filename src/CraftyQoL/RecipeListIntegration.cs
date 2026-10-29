using System;
using System.Collections;
using System.Collections.Generic;
using CraftyQoL.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CraftyQoL
{
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    internal static class RecipeListPatch
    {
        private static long _refreshes;
        internal struct Trace { internal bool Active; internal int Source; internal long Started; }
        private static void Prefix(InventoryGui __instance, ref List<Recipe> __0, out Trace __state)
        {
            __state = default;
            FoodView? view = FoodView.Current;
            if (!view || !view!.IsFor(__instance)) return;
            try
            {
                if (StationScope.Identify(__instance) == null) return;
                if (Plugin.Instance != null && Plugin.Instance.LogRefreshes.Value)
                    __state = new Trace { Active = true, Source = __0.Count, Started = System.Diagnostics.Stopwatch.GetTimestamp() };
                __0 = view.Filter(__0);
            }
            catch (Exception error) { Plugin.Instance?.Fail(error); }
        }
        private static void Postfix(InventoryGui __instance, Trace __state)
        {
            FoodView? view = FoodView.Current;
            if (!view || !view!.IsFor(__instance)) return;
            try
            {
                if (StationScope.Identify(__instance) == null) return;
                view.RowsPopulated();
                if (__state.Active) Plugin.Instance?.ReportRefresh(++_refreshes, __state.Source,
                    ((IList)GameContracts.Pairs.GetValue(__instance)).Count,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - __state.Started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            catch (Exception error) { Plugin.Instance?.Fail(error); }
        }
    }
    internal readonly struct RecipeRow
    {
        internal readonly object Pair;
        internal readonly Recipe Recipe;
        internal readonly RecipeInfo Info;
        internal readonly GameObject Element;
        internal RecipeRow(object pair, Recipe recipe, RecipeInfo info, GameObject element)
        { Pair = pair; Recipe = recipe; Info = info; Element = element; }
    }
}
