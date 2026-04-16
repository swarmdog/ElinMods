using BepInEx.Configuration;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldConfig
    {
        internal static ConfigEntry<int> PotencyBaseMultiplier;
        internal static ConfigEntry<int> ToxicityBaseMultiplier;
        internal static ConfigEntry<float> SkillPotencyPerLevel;
        internal static ConfigEntry<int> FlourPotencyMultiplier;
        internal static ConfigEntry<int> FlourToxicityMultiplier;
        internal static ConfigEntry<int> WaterPotencyMultiplier;
        internal static ConfigEntry<int> WaterToxicityMultiplier;
        internal static ConfigEntry<int> ProcessingVatDaysBase;
        internal static ConfigEntry<int> ProcessingVatQualityBonus;
        internal static ConfigEntry<bool> EnablePCSelfAddiction;
        internal static ConfigEntry<float> DrugDurationMultiplier;
        internal static ConfigEntry<float> CrashSeverityMultiplier;
        internal static ConfigEntry<bool> SmokeParticleEnabled;
        internal static ConfigEntry<float> RawHerbValueMultiplier;
        internal static ConfigEntry<int> FrostbloomSeedChance;
        internal static ConfigEntry<int> AshveilSeedChance;
        internal static ConfigEntry<float> StrainLevelingBonus;
        internal static ConfigEntry<float> DrugStatBonusMultiplier;
        internal static ConfigEntry<int> SampleAcceptChanceBase;
        internal static ConfigEntry<int> DealingPayoutMultiplier;
        internal static ConfigEntry<int> GuardDetectionRadius;
        internal static ConfigEntry<int> GuardNearbyOfferPenalty;
        internal static ConfigEntry<int> RefusalCooldownHours;
        internal static ConfigEntry<float> ContrabandDetectionBase;
        internal static ConfigEntry<float> ContrabandStealthReduction;
        internal static ConfigEntry<float> ContrabandNegotiationReduction;
        internal static ConfigEntry<float> ContrabandDetectionFloor;
        internal static ConfigEntry<float> ContrabandDetectionCeiling;
        internal static ConfigEntry<int> NearbyGuardDealDetectionBase;
        internal static ConfigEntry<float> NearbyGuardDealStealthReduction;
        internal static ConfigEntry<float> AddictionGainPerPotency;
        internal static ConfigEntry<float> ToleranceGainPerPotency;
        internal static ConfigEntry<bool> AddictionNaturalDecayEnabled;
        internal static ConfigEntry<float> AddictionPriceBonusPerPoint;
        internal static ConfigEntry<int> ODAddictionThreshold;
        internal static ConfigEntry<float> ODBaseChance;
        internal static ConfigEntry<float> ODPotencyFactor;
        internal static ConfigEntry<float> ODToxicityFactor;
        internal static ConfigEntry<float> ODMaxChance;
        internal static ConfigEntry<float> ODFatalChance;
        internal static ConfigEntry<int> ODSevereHeatGain;
        internal static ConfigEntry<int> ODSevereGuardAlertChance;
        internal static ConfigEntry<int> ODFatalHeatGain;
        internal static ConfigEntry<int> ODFatalKarmaPenalty;
        internal static ConfigEntry<float> CustomerFlightChance;
        internal static ConfigEntry<int> ODFatalRepPenalty;
        internal static ConfigEntry<int> RepGainPerSample;
        internal static ConfigEntry<int> RepGainPerDeal;
        internal static ConfigEntry<int> FixerRecruitMinRank;

        internal static void Bind(ConfigFile config)
        {
            PotencyBaseMultiplier = config.Bind("Crafting", "PotencyBaseMultiplier", 100, "Percent multiplier on base potency for all crafted underworld products.");
            ToxicityBaseMultiplier = config.Bind("Crafting", "ToxicityBaseMultiplier", 100, "Percent multiplier on base toxicity for all crafted underworld products.");
            SkillPotencyPerLevel = config.Bind("Crafting", "SkillPotencyPerLevel", 0.5f, "Potency bonus per crafting skill level.");
            FlourPotencyMultiplier = config.Bind("CuttingAgents", "FlourPotencyMultiplier", 70, "Percent potency retained when cutting with flour.");
            FlourToxicityMultiplier = config.Bind("CuttingAgents", "FlourToxicityMultiplier", 90, "Percent toxicity retained when cutting with flour.");
            WaterPotencyMultiplier = config.Bind("CuttingAgents", "WaterPotencyMultiplier", 80, "Percent potency retained when cutting with water.");
            WaterToxicityMultiplier = config.Bind("CuttingAgents", "WaterToxicityMultiplier", 85, "Percent toxicity retained when cutting with water.");
            ProcessingVatDaysBase = config.Bind("Processing", "ProcessingVatDaysBase", 3, "Base in-game days used for vat progression tuning.");
            ProcessingVatQualityBonus = config.Bind("Processing", "ProcessingVatQualityBonus", 20, "Percent potency bonus for vat processed products.");
            EnablePCSelfAddiction = config.Bind("PlayerEffects", "EnablePCSelfAddiction", false, "Enable self-addiction from personal drug use.");
            DrugDurationMultiplier = config.Bind("PlayerEffects", "DrugDurationMultiplier", 1.0f, "Global multiplier on underworld drug condition duration.");
            CrashSeverityMultiplier = config.Bind("PlayerEffects", "CrashSeverityMultiplier", 1.0f, "Global multiplier on underworld crash severity and duration.");
            SmokeParticleEnabled = config.Bind("PlayerEffects", "SmokeParticleEnabled", true, "Enable smoke particles while under smoking conditions.");
            RawHerbValueMultiplier = config.Bind("Economy", "RawHerbValueMultiplier", 1.0f, "Multiplier on raw herb value.");
            FrostbloomSeedChance = config.Bind("Cultivation", "FrostbloomSeedChance", 15, "Percent chance to discover Frostbloom seeds in Noyel or during winter.");
            AshveilSeedChance = config.Bind("Cultivation", "AshveilSeedChance", 15, "Percent chance to discover Ashveil seeds in Lothria.");
            StrainLevelingBonus = config.Bind("Cultivation", "StrainLevelingBonus", 1.0f, "Multiplier on seed-leveling effects for harvested crops.");
            DrugStatBonusMultiplier = config.Bind("PlayerEffects", "DrugStatBonusMultiplier", 1.0f, "Global multiplier on stat bonuses granted by underworld conditions.");
            SampleAcceptChanceBase = config.Bind("Dealing", "SampleAcceptChanceBase", 50, "Base percent chance an eligible NPC accepts a sample before skills and archetype modifiers.");
            DealingPayoutMultiplier = config.Bind("Dealing", "DealingPayoutMultiplier", 100, "Percent multiplier on direct customer payouts.");
            GuardDetectionRadius = config.Bind("Dealing", "GuardDetectionRadius", 5, "Tile radius within which guards can notice hand-to-hand dealing.");
            GuardNearbyOfferPenalty = config.Bind("Dealing", "GuardNearbyOfferPenalty", 10, "Acceptance penalty applied when guards are nearby.");
            RefusalCooldownHours = config.Bind("Dealing", "RefusalCooldownHours", 72, "Hours before a refused prospect will entertain another sample.");
            ContrabandDetectionBase = config.Bind("Concealment", "ContrabandDetectionBase", 60f, "Base percent chance exposed contraband is detected in lawful zones.");
            ContrabandStealthReduction = config.Bind("Concealment", "ContrabandStealthReduction", 1.5f, "Detection chance reduced per stealth level.");
            ContrabandNegotiationReduction = config.Bind("Concealment", "ContrabandNegotiationReduction", 0.5f, "Detection chance reduced per negotiation level.");
            ContrabandDetectionFloor = config.Bind("Concealment", "ContrabandDetectionFloor", 5f, "Minimum detection chance for exposed contraband.");
            ContrabandDetectionCeiling = config.Bind("Concealment", "ContrabandDetectionCeiling", 95f, "Maximum detection chance for exposed contraband.");
            NearbyGuardDealDetectionBase = config.Bind("Concealment", "NearbyGuardDealDetectionBase", 20, "Base percent chance a nearby guard notices an active deal.");
            NearbyGuardDealStealthReduction = config.Bind("Concealment", "NearbyGuardDealStealthReduction", 2f, "Nearby-guard deal notice chance reduced per stealth level.");
            AddictionGainPerPotency = config.Bind("Addiction", "AddictionGainPerPotency", 0.3f, "Addiction gained per point of product potency on a successful sale.");
            ToleranceGainPerPotency = config.Bind("Addiction", "ToleranceGainPerPotency", 0.1f, "Tolerance gained per point of potency when product strength exceeds current tolerance.");
            AddictionNaturalDecayEnabled = config.Bind("Addiction", "AddictionNaturalDecayEnabled", false, "If enabled, customer addiction decays by one per in-game day without service.");
            AddictionPriceBonusPerPoint = config.Bind("Addiction", "AddictionPriceBonusPerPoint", 0.005f, "Price bonus per addiction point above 30.");
            ODAddictionThreshold = config.Bind("Overdose", "ODAddictionThreshold", 61, "Minimum addiction at which overdose checks begin.");
            ODBaseChance = config.Bind("Overdose", "ODBaseChance", 0.02f, "Base overdose chance before potency, toxicity, and addiction modifiers.");
            ODPotencyFactor = config.Bind("Overdose", "ODPotencyFactor", 0.005f, "Overdose chance added per point of potency above tolerance demand.");
            ODToxicityFactor = config.Bind("Overdose", "ODToxicityFactor", 0.10f, "Overdose chance contribution from toxicity.");
            ODMaxChance = config.Bind("Overdose", "ODMaxChance", 0.40f, "Maximum overdose chance from any single sale.");
            ODFatalChance = config.Bind("Overdose", "ODFatalChance", 0.15f, "Share of overdose results that are fatal.");
            ODSevereHeatGain = config.Bind("Overdose", "ODSevereHeatGain", 5, "Heat gained when a customer suffers a severe overdose.");
            ODSevereGuardAlertChance = config.Bind("Overdose", "ODSevereGuardAlertChance", 20, "Chance a severe overdose draws guard attention.");
            ODFatalHeatGain = config.Bind("Overdose", "ODFatalHeatGain", 15, "Heat gained when a customer dies from an overdose.");
            ODFatalKarmaPenalty = config.Bind("Overdose", "ODFatalKarmaPenalty", -5, "Karma applied when a customer dies from an overdose.");
            CustomerFlightChance = config.Bind("Overdose", "CustomerFlightChance", 0.30f, "Chance nearby customers lose faith after a fatal overdose.");
            ODFatalRepPenalty = config.Bind("Overdose", "ODFatalRepPenalty", -20, "Local territory rep change on fatal overdose.");
            RepGainPerSample = config.Bind("Dealing", "RepGainPerSample", 1, "Territory rep gained when a prospect accepts a sample.");
            RepGainPerDeal = config.Bind("Dealing", "RepGainPerDeal", 1, "Territory rep gained when a standing order is fulfilled.");
            FixerRecruitMinRank = config.Bind("Fixer", "RecruitMinRank", 1, "Minimum underworld rank to recruit the Fixer (0=Novice, 1=Peddler, 2=Supplier, 3=Kingpin, 4=Shadow Lord).");
        }
    }
}
