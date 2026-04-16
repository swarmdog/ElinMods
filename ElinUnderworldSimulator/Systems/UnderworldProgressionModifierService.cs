using System;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldProgressionModifierService
    {
        internal static int ScaleExpGain(Card owner, int baseAmount)
        {
            if (owner?.Chara == null || baseAmount <= 0)
            {
                return baseAmount;
            }

            ConUWDreamHigh dreamHigh = owner.Chara.GetCondition<ConUWDreamHigh>();
            if (dreamHigh == null)
            {
                return baseAmount;
            }

            float multiplier = 1f + 0.20f * Math.Max(0.1f, dreamHigh.power / 100f);
            return Math.Max(baseAmount, (int)Math.Round(baseAmount * multiplier));
        }
    }
}
