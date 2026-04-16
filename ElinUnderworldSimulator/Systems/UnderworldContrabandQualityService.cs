using System;
using System.Collections.Generic;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldContrabandQualityService
    {
        private const string CraftingSkillAlias = "handicraft";
        private static int? _craftingSkillId;

        internal static bool IsContrabandProduct(string itemId)
        {
            return UnderworldDrugCatalog.TryGetProduct(itemId, out _);
        }

        internal static void ApplyCraftedProduct(Thing product, List<Thing> ingredients)
        {
            if (product == null || !UnderworldDrugCatalog.TryGetProduct(product.id, out UnderworldProductDefinition definition))
            {
                return;
            }

            BaseQualityValues baseValues = ReadBaseQualityValues(definition);
            int potency = ScalePercent(baseValues.Potency, UnderworldConfig.PotencyBaseMultiplier.Value);
            int toxicity = ScalePercent(baseValues.Toxicity, UnderworldConfig.ToxicityBaseMultiplier.Value);
            int traceability = baseValues.Traceability;

            if (ingredients != null && ingredients.Count > 0)
            {
                float totalQuality = 0f;
                int qualityCount = 0;

                foreach (Thing ingredient in ingredients)
                {
                    if (ingredient == null)
                    {
                        continue;
                    }

                    totalQuality += ingredient.Quality;
                    qualityCount++;
                    ApplyCuttingAgent(ingredient.id, ref potency, ref toxicity, ref traceability);
                }

                if (qualityCount > 0)
                {
                    float averageQuality = totalQuality / qualityCount;
                    float qualityMultiplier = 1f + Math.Max(0f, averageQuality - 1f) * 0.125f;
                    potency = (int)Math.Round(potency * qualityMultiplier);
                    toxicity = (int)Math.Round(toxicity / Math.Max(0.6f, qualityMultiplier));
                }
            }

            potency += GetCraftingSkillPotencyBonus();
            traceability += potency / 4;

            product.elements.SetBase(UnderworldContentIds.PotencyElement, Clamp(potency, 1, 100));
            product.elements.SetBase(UnderworldContentIds.ToxicityElement, Clamp(toxicity, 0, 100));
            product.elements.SetBase(UnderworldContentIds.TraceabilityElement, Clamp(traceability, 0, 50));
        }

        internal static void ApplyProcessedProduct(
            Thing product,
            string inputId,
            int inheritedPotency,
            int inheritedToxicity,
            int inheritedTraceability,
            int inheritedValue)
        {
            if (product == null)
            {
                return;
            }

            if (!UnderworldDrugCatalog.TryGetProduct(product.id, out UnderworldProductDefinition definition))
            {
                throw new InvalidOperationException(
                    $"Processed underworld product '{product.id}' from '{inputId}' is missing from the drug catalog.");
            }

            BaseQualityValues baseValues = ReadBaseQualityValues(definition);
            int potency = Math.Max(baseValues.Potency, inheritedPotency);
            int toxicity = inheritedToxicity > 0 ? Math.Min(baseValues.Toxicity, inheritedToxicity) : baseValues.Toxicity;
            int traceability = Math.Max(baseValues.Traceability, inheritedTraceability);

            potency += ScalePercent(Math.Max(10, potency), UnderworldConfig.ProcessingVatQualityBonus.Value);
            toxicity = Math.Max(0, toxicity - 2);
            traceability = Math.Min(50, traceability + 3);

            product.elements.SetBase(UnderworldContentIds.PotencyElement, Clamp(potency, 1, 100));
            product.elements.SetBase(UnderworldContentIds.ToxicityElement, Clamp(toxicity, 0, 100));
            product.elements.SetBase(UnderworldContentIds.TraceabilityElement, Clamp(traceability, 0, 50));

            product.c_priceAdd += Math.Max(inheritedValue, definition.BasePrice) / 2;
        }

        private static BaseQualityValues ReadBaseQualityValues(UnderworldProductDefinition definition)
        {
            return new BaseQualityValues
            {
                Potency = Math.Max(1, definition.BasePotency),
                Toxicity = Math.Max(0, definition.BaseToxicity),
                Traceability = Math.Max(0, definition.BaseTraceability),
            };
        }

        private static void ApplyCuttingAgent(string ingredientId, ref int potency, ref int toxicity, ref int traceability)
        {
            switch (ingredientId)
            {
                case "flour":
                    potency = ScalePercent(potency, UnderworldConfig.FlourPotencyMultiplier.Value);
                    toxicity = ScalePercent(toxicity, UnderworldConfig.FlourToxicityMultiplier.Value);
                    traceability = Math.Max(0, traceability - 1);
                    break;
                case "water":
                    potency = ScalePercent(potency, UnderworldConfig.WaterPotencyMultiplier.Value);
                    toxicity = ScalePercent(toxicity, UnderworldConfig.WaterToxicityMultiplier.Value);
                    traceability = Math.Max(0, traceability - 1);
                    break;
                case "ore_gem":
                    potency = ScalePercent(potency, 110);
                    traceability += 2;
                    break;
            }
        }

        private static int GetCraftingSkillPotencyBonus()
        {
            if (_craftingSkillId == null)
            {
                if (!EClass.sources.elements.alias.TryGetValue(CraftingSkillAlias, out SourceElement.Row craftingRow))
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve required vanilla '{CraftingSkillAlias}' element alias.");
                }

                _craftingSkillId = craftingRow.id;
            }

            return (int)Math.Round(EClass.pc.Evalue(_craftingSkillId.Value) * UnderworldConfig.SkillPotencyPerLevel.Value);
        }

        private static int ScalePercent(int value, int percent)
        {
            return (int)Math.Round(value * percent / 100f);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private sealed class BaseQualityValues
        {
            internal int Potency;
            internal int Toxicity;
            internal int Traceability;
        }
    }
}
