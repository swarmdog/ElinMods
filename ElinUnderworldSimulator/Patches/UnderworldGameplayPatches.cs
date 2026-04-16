using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ElinUnderworldSimulator
{
    [HarmonyPatch(typeof(Game), nameof(Game.OnUpdate))]
    internal static class Patch_Game_OnUpdate_UnderworldRuntime
    {
        private static void Postfix()
        {
            UnderworldRuntime.ProcessWorldTick();
        }
    }

    [HarmonyPatch(typeof(Player), "get_IsCriminal")]
    internal static class Patch_Player_get_IsCriminal_UnderworldContraband
    {
        private static void Postfix(ref bool __result)
        {
            if (__result || EClass.pc == null || EClass.pc.HasCondition<ConIncognito>())
            {
                return;
            }

            if (UnderworldRuntime.IsLawfulZone(EClass._zone) && UnderworldRuntime.IsContrabandDetected)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(RecipeCard), nameof(RecipeCard.Craft))]
    internal static class Patch_RecipeCard_Craft_UnderworldQuality
    {
        private static void Postfix(List<Thing> ings, Thing __result)
        {
            UnderworldContrabandQualityService.ApplyCraftedProduct(__result, ings);
        }
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.TryUse))]
    internal static class Patch_Chara_TryUse_Smokeables
    {
        private static bool Prefix(Chara __instance, Thing t, ref bool __result)
        {
            if (t == null
                || !UnderworldDrugCatalog.TryGetConsumptionProfile(t.id, out UnderworldConsumptionProfile profile)
                || !profile.AllowsRoute(UnderworldConsumptionRoute.Smoke))
            {
                return true;
            }

            UnderworldConsumptionService.Apply(new UnderworldConsumptionContext(__instance, t, UnderworldConsumptionRoute.Smoke));
            t.ModNum(-1);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.Drink))]
    internal static class Patch_Chara_Drink_UnderworldProducts
    {
        private static bool Prefix(Chara __instance, Card t)
        {
            if (t is not Thing thing
                || !UnderworldDrugCatalog.TryGetConsumptionProfile(thing.id, out UnderworldConsumptionProfile profile)
                || !profile.AllowsRoute(UnderworldConsumptionRoute.Drink))
            {
                return true;
            }

            __instance.Say("drink", __instance, thing.Duplicate(1));
            __instance.Say("quaff");
            __instance.hunger.Mod(-2);
            thing.ModNum(-1);
            UnderworldConsumptionService.Apply(new UnderworldConsumptionContext(__instance, thing, UnderworldConsumptionRoute.Drink));
            return false;
        }
    }

    [HarmonyPatch(typeof(FoodEffect), nameof(FoodEffect.ProcTrait))]
    internal static class Patch_FoodEffect_ProcTrait_UnderworldTraits
    {
        private static void Postfix(Chara c, Card t)
        {
            UnderworldConsumptionService.TryApplyFoodTraits(c, t);
        }
    }

    [HarmonyPatch(typeof(AttackProcess), nameof(AttackProcess.GetRawDamage))]
    internal static class Patch_AttackProcess_GetRawDamage_UnderworldCombat
    {
        private static void Postfix(AttackProcess __instance, ref long __result)
        {
            __result = UnderworldCombatModifierService.ApplyMeleeDamageModifier(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.ModExp), typeof(int), typeof(int))]
    internal static class Patch_Card_ModExp_UnderworldProgression
    {
        private static void Prefix(Card __instance, int ele, ref int a)
        {
            if (a > 0)
            {
                a = UnderworldProgressionModifierService.ScaleExpGain(__instance, a);
            }
        }
    }

    [HarmonyPatch(typeof(Chara), "_Move")]
    internal static class Patch_Chara_Move_SmokeParticles
    {
        private static void Postfix(Chara __instance)
        {
            if (!UnderworldConfig.SmokeParticleEnabled.Value || __instance == null || !__instance.IsPC || __instance.renderer == null)
            {
                return;
            }

            bool isSmoking = __instance.HasCondition<ConSmoking>()
                || __instance.HasCondition<ConUWWhisperCalm>()
                || __instance.HasCondition<ConUWDreamCalm>()
                || __instance.HasCondition<ConUWAshveil>();

            if (!isSmoking)
            {
                return;
            }

            PCOrbit pcOrbit = EClass.screen?.pcOrbit;
            Scene scene = EClass.scene;
            if (pcOrbit == null || scene == null || scene.psSmoke == null)
            {
                return;
            }

            scene.psSmoke.transform.position = __instance.renderer.position + pcOrbit.smokePos;
            scene.psSmoke.Emit(pcOrbit.emitSmoke);
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.OnVisit))]
    internal static class Patch_Zone_OnVisit_UnderworldSeeds
    {
        private static void Postfix(Zone __instance)
        {
            UnderworldRuntime.OnZoneVisited(__instance);
            UnderworldSeedSpawnService.TrySpawnRegionalSeed(__instance);
        }
    }

    [HarmonyPatch(typeof(TraitSeed), nameof(TraitSeed.LevelSeed))]
    internal static class Patch_TraitSeed_LevelSeed_Bonus
    {
        [ThreadStatic]
        private static bool _isApplyingBonus;
        // how broad is this patch? we need to make sure we aren't affecting non mod related crops
        private static void Postfix(Thing t, SourceObj.Row obj, int num)
        {
            if (_isApplyingBonus
                || t == null
                || obj == null
                || num <= 0
                || !UnderworldContentIds.CropIds.Contains(obj.alias))
            {
                return;
            }

            float bonusMultiplier = UnderworldConfig.StrainLevelingBonus.Value;
            int extraLevels = (int)Math.Round(num * Math.Max(0f, bonusMultiplier - 1f));
            if (extraLevels <= 0)
            {
                return;
            }

            try
            {
                _isApplyingBonus = true;
                TraitSeed.LevelSeed(t, obj, extraLevels);
            }
            finally
            {
                _isApplyingBonus = false;
            }
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.AddThing), typeof(Thing), typeof(bool), typeof(int), typeof(int))]
    internal static class Patch_Card_AddThing_UnderworldContraband
    {
        private static void Postfix(Card __instance, Thing t)
        {
            if (__instance == null || t == null)
            {
                return;
            }

            if (__instance?.trait is TraitProcessingVat)
            {
                UnderworldProcessingVatService.StampInsertedAt(t);
            }

            Card root = __instance.IsPC ? __instance : __instance.GetRootCard();
            if (root == EClass.pc)
            {
                UnderworldRuntime.HandleInventoryMutation(t);
            }
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.RemoveThing))]
    internal static class Patch_Card_RemoveThing_UnderworldContraband
    {
        private static void Postfix(Card __instance, Thing thing)
        {
            if (__instance == null || thing == null)
            {
                return;
            }

            if (__instance?.trait is TraitProcessingVat)
            {
                UnderworldProcessingVatService.ClearProgress(thing);
            }

            Card root = __instance.IsPC ? __instance : __instance.GetRootCard();
            if (root == EClass.pc)
            {
                UnderworldRuntime.HandleInventoryMutation(thing);
            }
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.GetValue))]
    internal static class Patch_Card_GetValue_UnderworldEconomy
    {
        private static void Postfix(Card __instance, PriceType priceType, bool sell, ref int __result)
        {
            if (!sell || __instance is not Thing thing || !UnderworldContentIds.RawHerbItemIds.Contains(thing.id) || __result <= 0)
            {
                return;
            }

            __result = Mathf.Max(1, Mathf.RoundToInt(__result * UnderworldConfig.RawHerbValueMultiplier.Value));
        }
    }
}
