using System.Collections.Generic;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldContentIds
    {
        internal const string FixerId = "uw_fixer";

        internal const string MixingTableId = "uw_mixing_table";
        internal const string ProcessingVatId = "uw_processing_vat";
        internal const string AdvancedLabId = "uw_advanced_lab";
        internal const string ContrabandChestId = "uw_contraband_chest";
        internal const string DealerLedgerId = "uw_dealers_ledger";
        internal const string SampleKitId = "uw_sample_kit";
        internal const string AntidoteId = "uw_antidote_vial";
        internal const string TerritoryMapId = "uw_territory_map";
        internal const string FactionDeskId = "uw_faction_desk";
        internal const string DeadDropBoardId = "uw_dead_drop_board";
        internal const string HeatMonitorId = "uw_heat_monitor";

        internal const string HerbWhisperId = "uw_herb_whisper";
        internal const string HerbDreamId = "uw_herb_dream";
        internal const string HerbShadowId = "uw_herb_shadow";
        internal const string HerbCrimsonId = "uw_herb_crimson";
        internal const string HerbFrostbloomId = "uw_herb_frostbloom";
        internal const string HerbAshveilId = "uw_herb_ashveil";
        internal const string MineralCrudeId = "uw_mineral_crude";
        internal const string MineralCrystalId = "uw_mineral_crystal";

        internal const string CropWhisperId = "uw_crop_whisper";
        internal const string CropDreamId = "uw_crop_dream";
        internal const string CropShadowId = "uw_crop_shadow";
        internal const string CropCrimsonId = "uw_crop_crimson";
        internal const string CropFrostbloomId = "uw_crop_frostbloom";
        internal const string CropAshveilId = "uw_crop_ashveil";

        internal const string ExtractWhisperId = "uw_extract_whisper";
        internal const string ExtractDreamId = "uw_extract_dream";
        internal const string ExtractShadowId = "uw_extract_shadow";
        internal const string PowderMooniteId = "uw_powder_moonite";
        internal const string CrystalVoidId = "uw_crystal_void";

        internal const string TonicWhisperId = "uw_tonic_whisper";
        internal const string PowderDreamId = "uw_powder_dream";
        internal const string ElixirShadowId = "uw_elixir_shadow";
        internal const string SaltsVoidId = "uw_salts_void";
        internal const string ElixirCrimsonId = "uw_elixir_crimson";
        internal const string RollWhisperId = "uw_roll_whisper";
        internal const string RollDreamId = "uw_roll_dream";
        internal const string DraughtBerserkerId = "uw_draught_berserker";
        internal const string ElixirRushId = "uw_elixir_rush";
        internal const string ElixirFrostId = "uw_elixir_frost";
        internal const string IncenseAshId = "uw_incense_ash";

        internal const string RefinedWhisperId = "uw_tonic_whisper_refined";
        internal const string RefinedDreamId = "uw_powder_dream_refined";
        internal const string RefinedShadowId = "uw_elixir_shadow_refined";
        internal const string AgedWhisperId = "uw_tonic_whisper_aged";
        internal const string ConcentratedDreamId = "uw_powder_dream_concentrated";

        internal const string ConWhisperHigh = "ConUWWhisperHigh";
        internal const string ConShadowRush = "ConUWShadowRush";
        internal const string ConShadowCrash = "ConUWShadowCrash";
        internal const string ConDreamHigh = "ConUWDreamHigh";
        internal const string ConVoidRage = "ConUWVoidRage";
        internal const string ConCrimsonSurge = "ConUWCrimsonSurge";
        internal const string ConWhisperCalm = "ConUWWhisperCalm";
        internal const string ConDreamCalm = "ConUWDreamCalm";
        internal const string ConBerserkerRage = "ConUWBerserkerRage";
        internal const string ConBerserkerCrash = "ConUWBerserkerCrash";
        internal const string ConShadowRushX = "ConUWShadowRushX";
        internal const string ConRushCrash = "ConUWRushCrash";
        internal const string ConFrostbloom = "ConUWFrostbloom";
        internal const string ConAshveil = "ConUWAshveil";
        internal const string ConWithdrawal = "ConUWWithdrawal";
        internal const string ConOverdose = "ConUWOverdose";

        internal const int CustomerAddictionElement = 90010;
        internal const int CustomerToleranceElement = 90011;
        internal const int CustomerLoyaltyElement = 90012;
        internal const int CustomerPreferredProductElement = 90013;
        internal const int CustomerOfferCooldownElement = 90014;
        internal const int PotencyElement = 90020;
        internal const int DreamTraitElement = 90021;
        internal const int VoidTraitElement = 90022;
        internal const int ToxicityElement = 90023;
        internal const int TraceabilityElement = 90024;
        internal const int VatStartRawElement = 90025;

        internal const int FrostbloomCropRefVal = 90104;
        internal const int AshveilCropRefVal = 90105;

        internal static readonly HashSet<string> StationIds = new HashSet<string>
        {
            MixingTableId,
            ProcessingVatId,
            AdvancedLabId,
        };

        internal static readonly HashSet<string> FurnitureIds = new HashSet<string>
        {
            TerritoryMapId,
            FactionDeskId,
            DeadDropBoardId,
            HeatMonitorId,
        };

        internal static readonly HashSet<string> SmokeableItemIds = new HashSet<string>
        {
            RollWhisperId,
            RollDreamId,
            PowderDreamId,
            IncenseAshId,
        };

        internal static readonly HashSet<string> LiquidDrugIds = new HashSet<string>
        {
            TonicWhisperId,
            ElixirShadowId,
            ElixirCrimsonId,
            DraughtBerserkerId,
            ElixirRushId,
            ElixirFrostId,
            RefinedWhisperId,
            RefinedShadowId,
            AgedWhisperId,
        };

        internal static readonly HashSet<string> CropIds = new HashSet<string>
        {
            CropWhisperId,
            CropDreamId,
            CropShadowId,
            CropCrimsonId,
            CropFrostbloomId,
            CropAshveilId,
        };

        internal static readonly HashSet<string> RawHerbItemIds = new HashSet<string>
        {
            HerbWhisperId,
            HerbDreamId,
            HerbShadowId,
            HerbCrimsonId,
            HerbFrostbloomId,
            HerbAshveilId,
        };

        internal static readonly Dictionary<string, string> VatProcessingMap = new Dictionary<string, string>
        {
            { ExtractWhisperId, RefinedWhisperId },
            { ExtractDreamId, RefinedDreamId },
            { ExtractShadowId, RefinedShadowId },
            { TonicWhisperId, AgedWhisperId },
            { PowderDreamId, ConcentratedDreamId },
        };
    }
}
