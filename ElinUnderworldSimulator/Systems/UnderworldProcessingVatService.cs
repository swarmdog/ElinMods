using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldVatRecipe
    {
        internal string InputId;
        internal string OutputId;
        internal float DayMultiplier;
    }

    internal static class UnderworldProcessingVatService
    {
        private const int MinutesPerDay = 1440;

        private static readonly Dictionary<string, UnderworldVatRecipe> Recipes = new Dictionary<string, UnderworldVatRecipe>
        {
            { UnderworldContentIds.ExtractWhisperId, CreateRecipe(UnderworldContentIds.ExtractWhisperId, UnderworldContentIds.RefinedWhisperId, 1) },
            { UnderworldContentIds.ExtractDreamId, CreateRecipe(UnderworldContentIds.ExtractDreamId, UnderworldContentIds.RefinedDreamId, 4f / 3f) },
            { UnderworldContentIds.ExtractShadowId, CreateRecipe(UnderworldContentIds.ExtractShadowId, UnderworldContentIds.RefinedShadowId, 5f / 3f) },
            { UnderworldContentIds.TonicWhisperId, CreateRecipe(UnderworldContentIds.TonicWhisperId, UnderworldContentIds.AgedWhisperId, 2) },
            { UnderworldContentIds.PowderDreamId, CreateRecipe(UnderworldContentIds.PowderDreamId, UnderworldContentIds.ConcentratedDreamId, 7f / 3f) },
        };

        internal static bool CanProcess(Card card)
        {
            return card != null && Recipes.ContainsKey(card.id);
        }

        internal static bool TryGetRecipe(string inputId, out UnderworldVatRecipe recipe)
        {
            return Recipes.TryGetValue(inputId, out recipe);
        }

        internal static void StampInsertedAt(Thing item)
        {
            if (item == null || !TryGetRecipe(item.id, out _) || EClass.world?.date == null)
            {
                return;
            }

            if (item.Evalue(UnderworldContentIds.VatStartRawElement) <= 0)
            {
                item.elements.SetBase(UnderworldContentIds.VatStartRawElement, EClass.world.date.GetRaw());
            }
        }

        internal static bool IsReady(Thing item)
        {
            if (item == null || EClass.world?.date == null || !TryGetRecipe(item.id, out UnderworldVatRecipe recipe))
            {
                return false;
            }

            int startedAt = item.Evalue(UnderworldContentIds.VatStartRawElement);
            if (startedAt <= 0)
            {
                StampInsertedAt(item);
                return false;
            }

            int requiredMinutes = Mathf.Max(1, Mathf.RoundToInt(UnderworldConfig.ProcessingVatDaysBase.Value * recipe.DayMultiplier * MinutesPerDay));
            int elapsed = Math.Max(0, EClass.world.date.GetRaw() - startedAt);
            return elapsed >= requiredMinutes;
        }

        internal static void HoldPendingDecay(Thing item)
        {
            if (item == null)
            {
                return;
            }

            item.decay = Mathf.Max(0, item.MaxDecay - 20);
        }

        internal static void ClearProgress(Thing item)
        {
            if (item?.elements != null)
            {
                item.elements.SetBase(UnderworldContentIds.VatStartRawElement, 0);
            }
        }

        private static UnderworldVatRecipe CreateRecipe(string inputId, string outputId, float dayMultiplier)
        {
            return new UnderworldVatRecipe
            {
                InputId = inputId,
                OutputId = outputId,
                DayMultiplier = dayMultiplier,
            };
        }
    }
}
