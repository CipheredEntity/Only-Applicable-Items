using System;
using System.Collections.Generic;
using System.Reflection;
using OnlyApplicableItems.Core;
using UnityEngine;

namespace OnlyApplicableItems.Integration;

internal sealed class AlchemyContract
{
    public MethodInfo IngredientPredicate { get; }
    public MethodInfo CountUpdate { get; }
    public MethodInfo Hide { get; }
    private readonly MethodInfo redraw;
    private readonly FieldInfo ingredients;
    private readonly AlchemyVisibility<Item, SurveyDef> visibility;

    public AlchemyContract()
    {
        IngredientPredicate = RequireMethod("IsItemValidForMix", typeof(bool), typeof(Item));
        CountUpdate = RequireMethod("UpdateCountInWindow", typeof(void));
        Hide = RequireMethod("Hide", typeof(void));
        redraw = RequireMethod("RedrawAlchemyTabLite", typeof(void), typeof(AlchemyMixDef));
        ingredients = typeof(UIAlchemyWindow).GetField("ingredients", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(UIAlchemyWindow).FullName, "ingredients");
        if (ingredients.FieldType != typeof(List<UIAlchemyIngredient>))
            throw new NotSupportedException("The laboratory ingredient-slot representation has changed.");
        visibility = new AlchemyVisibility<Item, SurveyDef>(
            item => !item.IsEmpty,
            item => item.Definition.canBeUsedInAlchemy,
            item => item.Definition.GetRunesAsVector3Int() != Vector3Int.zero,
            item => GameBalance.GetSurveyDefForItemOrNull(item.Definition.id),
            survey => MainGame.Instance.GameSave.knowledgeSystem.IsSurveyCompleted(survey));
    }

    public bool CanDisplayIngredient(Item item) => visibility.CanDisplay(item);

    public List<UIItemCell> GetPopulatedCells(UIAlchemyWindow window)
    {
        var slots = (List<UIAlchemyIngredient>?)ingredients.GetValue(window)
            ?? throw new InvalidOperationException("The laboratory ingredient list is unavailable.");
        var cells = new List<UIItemCell>();
        foreach (var slot in slots)
        {
            if (slot != null && slot.gameObject.activeSelf && slot.cell != null &&
                slot.cell.DisplayingItem != null && !slot.cell.DisplayingItem.IsEmpty)
                cells.Add(slot.cell);
        }
        return cells;
    }

    public void Redraw(UIAlchemyWindow window)
    {
        // This method also redraws the result after UpdateCountInWindow multiplies its
        // cached counter. Invoking just the count helper could leave the result label squared.
        redraw.Invoke(window, new object?[] { null });
    }

    private static MethodInfo RequireMethod(string name, Type result, params Type[] arguments)
    {
        var method = typeof(UIAlchemyWindow).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(typeof(UIAlchemyWindow).FullName, name);
        return method;
    }
}
