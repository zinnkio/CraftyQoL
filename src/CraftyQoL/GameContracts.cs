using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CraftyQoL
{
    internal static class GameContracts
    {
        internal static FieldInfo Pairs = null!;
        internal static PropertyInfo PairRecipe = null!;
        internal static PropertyInfo PairElement = null!;
        internal static FieldInfo CraftTimer = null!;
        internal static FieldInfo BaseHeight = null!;
        internal static FieldInfo SelectedPair = null!;
        internal static MethodInfo RefreshPanel = null!;
        internal static MethodInfo SelectRecipe = null!;
        internal static MethodInfo RebuildList = null!;

        internal static void Resolve()
        {
            RefreshPanel = Method(typeof(InventoryGui), "UpdateCraftingPanel", typeof(bool));
            SelectRecipe = Method(typeof(InventoryGui), "SetRecipe", typeof(int), typeof(bool));
            RebuildList = Method(typeof(InventoryGui), "UpdateRecipeList", typeof(List<Recipe>));
            Method(typeof(InventoryGui), "Hide");
            Method(typeof(InventoryGui), "OnDestroy");
            Method(typeof(InventoryGui), "Update");
            Method(typeof(ZInput), "GetButtonDown", typeof(string));
            Method(typeof(ZInput), "GetKeyDown", typeof(KeyCode), typeof(bool));
            Method(typeof(UIGamePad), "ButtonPressed");
            Method(typeof(PlayerController), "TakeInput", typeof(bool));
            Method(typeof(Player), "IsBiomeKnown", typeof(BiomeSector));
            Pairs = Field("m_availableRecipes");
            CraftTimer = Field("m_craftTimer");
            BaseHeight = Field("m_recipeListBaseSize");
            SelectedPair = Field("m_selectedRecipe");
            if (CraftTimer.FieldType != typeof(float) || BaseHeight.FieldType != typeof(float))
                throw new NotSupportedException("Unexpected crafting timer/list geometry types.");
            Type pair = typeof(InventoryGui).GetNestedType("RecipeDataPair", BindingFlags.NonPublic)
                        ?? throw new MissingMemberException("InventoryGui.RecipeDataPair");
            if (!pair.IsValueType || Pairs.FieldType != typeof(List<>).MakeGenericType(pair))
                throw new NotSupportedException("Unexpected native recipe pair layout.");
            if (SelectedPair.FieldType != pair) throw new NotSupportedException("Unexpected selected-recipe layout.");
            PairRecipe = pair.GetProperty("Recipe") ?? throw new MissingMemberException("RecipeDataPair.Recipe");
            PairElement = pair.GetProperty("InterfaceElement") ?? throw new MissingMemberException("RecipeDataPair.InterfaceElement");
            if (PairRecipe.PropertyType != typeof(Recipe) || PairElement.PropertyType != typeof(GameObject))
                throw new NotSupportedException("Unexpected recipe pair property types.");
        }

        internal static MethodInfo Method(Type type, string name, params Type[] parameters)
            => AccessTools.DeclaredMethod(type, name, parameters)
               ?? throw new MissingMethodException(type.FullName, name);

        private static FieldInfo Field(string name)
            => AccessTools.DeclaredField(typeof(InventoryGui), name)
               ?? throw new MissingFieldException(typeof(InventoryGui).FullName, name);
    }
}
