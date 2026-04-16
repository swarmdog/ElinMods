using System;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldCombatModifierService
    {
        internal static long ApplyMeleeDamageModifier(AttackProcess attackProcess, long rawDamage)
        {
            if (attackProcess?.CC == null || rawDamage <= 0 || attackProcess.IsRanged || attackProcess.isThrow)
            {
                return rawDamage;
            }

            float multiplier = 1f;
            multiplier += GetConditionBonus<ConUWVoidRage>(attackProcess.CC, 0.25f);
            multiplier += GetConditionBonus<ConUWBerserkerRage>(attackProcess.CC, 0.20f);
            multiplier += GetConditionBonus<ConUWShadowRushX>(attackProcess.CC, 0.15f);

            if (multiplier <= 1f)
            {
                return rawDamage;
            }

            return Math.Max(1L, (long)Math.Round(rawDamage * multiplier));
        }

        private static float GetConditionBonus<T>(Chara user, float baseBonus) where T : UnderworldDrugCondition
        {
            T condition = user?.GetCondition<T>();
            if (condition == null)
            {
                return 0f;
            }

            return baseBonus * Math.Max(0.1f, condition.power / 100f);
        }
    }
}
