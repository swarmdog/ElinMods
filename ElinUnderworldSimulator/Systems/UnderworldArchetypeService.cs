using System;

namespace ElinUnderworldSimulator
{
    internal enum NpcArchetype
    {
        Laborer,
        Adventurer,
        Noble,
        Scholar,
        Rogue,
        Guard,
    }

    internal static class UnderworldArchetypeService
    {
        internal static NpcArchetype Classify(Chara customer)
        {
            if (customer == null)
            {
                return NpcArchetype.Adventurer;
            }

            if (customer.trait is TraitGuard)
            {
                return NpcArchetype.Guard;
            }

            if (customer.trait is TraitMerchant || customer.trait is TraitMayor || customer.trait is TraitElder || customer.IsWealthy)
            {
                return NpcArchetype.Noble;
            }

            if ((customer.trait is TraitTrainer trainer && string.Equals(trainer.IDTrainer, "mind", StringComparison.OrdinalIgnoreCase))
                || customer.trait is TraitHealer)
            {
                return NpcArchetype.Scholar;
            }

            if (customer.trait is TraitRogue || string.Equals(customer.race?.id, "juere", StringComparison.OrdinalIgnoreCase))
            {
                return NpcArchetype.Rogue;
            }

            if (customer.trait is TraitCitizen)
            {
                return NpcArchetype.Laborer;
            }

            return NpcArchetype.Adventurer;
        }

        internal static int GetAcceptModifier(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Laborer:
                    return 10;
                case NpcArchetype.Noble:
                    return -25;
                case NpcArchetype.Scholar:
                    return -35;
                case NpcArchetype.Rogue:
                    return 20;
                case NpcArchetype.Guard:
                    return -999;
                default:
                    return 0;
            }
        }

        internal static float GetPayMultiplier(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Laborer:
                    return 0.6f;
                case NpcArchetype.Noble:
                    return 1.5f;
                case NpcArchetype.Scholar:
                    return 1.8f;
                case NpcArchetype.Rogue:
                    return 0.7f;
                case NpcArchetype.Guard:
                    return 0f;
                default:
                    return 0.8f;
            }
        }

        internal static string GetLabel(NpcArchetype archetype)
        {
            switch (archetype)
            {
                case NpcArchetype.Laborer:
                    return "laborer";
                case NpcArchetype.Noble:
                    return "noble";
                case NpcArchetype.Scholar:
                    return "scholar";
                case NpcArchetype.Rogue:
                    return "rogue";
                case NpcArchetype.Guard:
                    return "guard";
                default:
                    return "adventurer";
            }
        }
    }
}
