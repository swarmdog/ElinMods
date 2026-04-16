using System.Collections.Generic;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal sealed class UnderworldProductDefinition
    {
        internal string ItemId;
        internal string DisplayName;
        internal int BasePrice;
        internal int PotencyTier;
        internal int AddictionDelta;
        internal int BasePotency;
        internal int BaseToxicity;
        internal int BaseTraceability;
        internal bool Sampleable;
        internal bool FinishedProduct;
        internal UnderworldConsumptionProfile ConsumptionProfile;
    }

    internal static class UnderworldDrugCatalog
    {
        private static readonly Dictionary<string, int> ProductMarkers = new Dictionary<string, int>();
        private static readonly Dictionary<int, string> MarkerProducts = new Dictionary<int, string>();

        private static readonly Dictionary<string, UnderworldProductDefinition> Products = new Dictionary<string, UnderworldProductDefinition>
        {
            { UnderworldContentIds.ExtractWhisperId, CreateProduct(UnderworldContentIds.ExtractWhisperId, "Whispervine Extract", 120, 1, 0, 22, 8, 4) },
            { UnderworldContentIds.ExtractDreamId, CreateProduct(UnderworldContentIds.ExtractDreamId, "Dreamblossom Essence", 200, 1, 0, 28, 10, 5) },
            { UnderworldContentIds.ExtractShadowId, CreateProduct(UnderworldContentIds.ExtractShadowId, "Shadowcap Distillate", 160, 1, 0, 30, 12, 6) },
            { UnderworldContentIds.PowderMooniteId, CreateProduct(UnderworldContentIds.PowderMooniteId, "Moonite Powder", 100, 1, 0, 10, 2, 1) },
            { UnderworldContentIds.CrystalVoidId, CreateProduct(UnderworldContentIds.CrystalVoidId, "Void Crystal", 300, 2, 0, 18, 4, 2) },
            {
                UnderworldContentIds.TonicWhisperId,
                CreateProduct(
                    UnderworldContentIds.TonicWhisperId,
                    "Whisper Tonic",
                    500,
                    1,
                    1,
                    40,
                    10,
                    10,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.TonicWhisperId,
                        UnderworldContentIds.ConWhisperHigh,
                        40,
                        10,
                        120,
                        UnderworldConsumptionRoute.Drink))
            },
            {
                UnderworldContentIds.PowderDreamId,
                CreateProduct(
                    UnderworldContentIds.PowderDreamId,
                    "Dream Powder",
                    800,
                    2,
                    2,
                    55,
                    14,
                    12,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.PowderDreamId,
                        UnderworldContentIds.ConDreamHigh,
                        55,
                        14,
                        170,
                        immediateHookId: UnderworldConsumptionService.DreamBlendHookId,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke, UnderworldConsumptionRoute.Blend }))
            },
            {
                UnderworldContentIds.ElixirShadowId,
                CreateProduct(
                    UnderworldContentIds.ElixirShadowId,
                    "Shadow Elixir",
                    1200,
                    3,
                    3,
                    70,
                    20,
                    15,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.ElixirShadowId,
                        UnderworldContentIds.ConShadowRush,
                        70,
                        20,
                        95,
                        crashConditionId: UnderworldContentIds.ConShadowCrash,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Drink }))
            },
            {
                UnderworldContentIds.SaltsVoidId,
                CreateProduct(
                    UnderworldContentIds.SaltsVoidId,
                    "Void Salts",
                    2000,
                    3,
                    3,
                    78,
                    24,
                    18,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.SaltsVoidId,
                        UnderworldContentIds.ConVoidRage,
                        78,
                        24,
                        206,
                        UnderworldConsumptionRoute.Eat))
            },
            {
                UnderworldContentIds.ElixirCrimsonId,
                CreateProduct(
                    UnderworldContentIds.ElixirCrimsonId,
                    "Crimson Elixir",
                    3000,
                    4,
                    4,
                    86,
                    28,
                    22,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.ElixirCrimsonId,
                        UnderworldContentIds.ConCrimsonSurge,
                        86,
                        28,
                        232,
                        UnderworldConsumptionRoute.Drink))
            },
            {
                UnderworldContentIds.RollWhisperId,
                CreateProduct(
                    UnderworldContentIds.RollWhisperId,
                    "Whispervine Roll",
                    200,
                    1,
                    1,
                    24,
                    6,
                    6,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.RollWhisperId,
                        UnderworldContentIds.ConWhisperCalm,
                        24,
                        6,
                        54,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke }))
            },
            {
                UnderworldContentIds.RollDreamId,
                CreateProduct(
                    UnderworldContentIds.RollDreamId,
                    "Dreamweed Joint",
                    250,
                    1,
                    1,
                    30,
                    7,
                    7,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.RollDreamId,
                        UnderworldContentIds.ConDreamCalm,
                        30,
                        7,
                        70,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke }))
            },
            {
                UnderworldContentIds.DraughtBerserkerId,
                CreateProduct(
                    UnderworldContentIds.DraughtBerserkerId,
                    "Berserker's Draught",
                    4500,
                    5,
                    5,
                    96,
                    36,
                    26,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.DraughtBerserkerId,
                        UnderworldContentIds.ConBerserkerRage,
                        96,
                        36,
                        136,
                        crashConditionId: UnderworldContentIds.ConBerserkerCrash,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Drink }))
            },
            {
                UnderworldContentIds.ElixirRushId,
                CreateProduct(
                    UnderworldContentIds.ElixirRushId,
                    "Shadow Rush",
                    3500,
                    5,
                    5,
                    94,
                    34,
                    25,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.ElixirRushId,
                        UnderworldContentIds.ConShadowRushX,
                        94,
                        34,
                        67,
                        crashConditionId: UnderworldContentIds.ConRushCrash,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Drink }))
            },
            {
                UnderworldContentIds.ElixirFrostId,
                CreateProduct(
                    UnderworldContentIds.ElixirFrostId,
                    "Frostbloom Elixir",
                    1800,
                    3,
                    2,
                    68,
                    16,
                    14,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.ElixirFrostId,
                        UnderworldContentIds.ConFrostbloom,
                        68,
                        16,
                        216,
                        UnderworldConsumptionRoute.Drink))
            },
            {
                UnderworldContentIds.IncenseAshId,
                CreateProduct(
                    UnderworldContentIds.IncenseAshId,
                    "Ashveil Incense",
                    2000,
                    3,
                    2,
                    66,
                    15,
                    12,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.IncenseAshId,
                        UnderworldContentIds.ConAshveil,
                        66,
                        15,
                        116,
                        immediateHookId: UnderworldConsumptionService.AshveilCloudHookId,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke, UnderworldConsumptionRoute.Throw }))
            },
            {
                UnderworldContentIds.RefinedWhisperId,
                CreateProduct(
                    UnderworldContentIds.RefinedWhisperId,
                    "Refined Whisper Tonic",
                    1000,
                    2,
                    2,
                    60,
                    8,
                    14,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.RefinedWhisperId,
                        UnderworldContentIds.ConWhisperHigh,
                        60,
                        8,
                        160,
                        UnderworldConsumptionRoute.Drink))
            },
            {
                UnderworldContentIds.RefinedDreamId,
                CreateProduct(
                    UnderworldContentIds.RefinedDreamId,
                    "Refined Dream Powder",
                    1800,
                    3,
                    3,
                    76,
                    11,
                    16,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.RefinedDreamId,
                        UnderworldContentIds.ConDreamHigh,
                        76,
                        11,
                        212,
                        immediateHookId: UnderworldConsumptionService.DreamBlendHookId,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke, UnderworldConsumptionRoute.Blend }))
            },
            {
                UnderworldContentIds.RefinedShadowId,
                CreateProduct(
                    UnderworldContentIds.RefinedShadowId,
                    "Refined Shadow Elixir",
                    3600,
                    4,
                    4,
                    90,
                    16,
                    20,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.RefinedShadowId,
                        UnderworldContentIds.ConShadowRush,
                        90,
                        16,
                        115,
                        crashConditionId: UnderworldContentIds.ConShadowCrash,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Drink }))
            },
            {
                UnderworldContentIds.AgedWhisperId,
                CreateProduct(
                    UnderworldContentIds.AgedWhisperId,
                    "Aged Whisper Tonic",
                    900,
                    2,
                    2,
                    56,
                    7,
                    12,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.AgedWhisperId,
                        UnderworldContentIds.ConWhisperHigh,
                        56,
                        7,
                        152,
                        UnderworldConsumptionRoute.Drink))
            },
            {
                UnderworldContentIds.ConcentratedDreamId,
                CreateProduct(
                    UnderworldContentIds.ConcentratedDreamId,
                    "Concentrated Dream Powder",
                    1760,
                    3,
                    3,
                    74,
                    10,
                    15,
                    sampleable: true,
                    finishedProduct: true,
                    consumptionProfile: CreateConsumptionProfile(
                        UnderworldContentIds.ConcentratedDreamId,
                        UnderworldContentIds.ConDreamHigh,
                        74,
                        10,
                        208,
                        immediateHookId: UnderworldConsumptionService.DreamBlendHookId,
                        applyVanillaSmoking: true,
                        allowedRoutes: new[] { UnderworldConsumptionRoute.Smoke, UnderworldConsumptionRoute.Blend }))
            },
        };

        static UnderworldDrugCatalog()
        {
            int marker = 1;
            foreach (string itemId in Products.Keys.OrderBy(id => id))
            {
                ProductMarkers[itemId] = marker;
                MarkerProducts[marker] = itemId;
                marker++;
            }
        }

        internal static IEnumerable<UnderworldProductDefinition> AllProducts => Products.Values;

        internal static bool TryGetProduct(string itemId, out UnderworldProductDefinition definition)
        {
            return Products.TryGetValue(itemId, out definition);
        }

        internal static bool TryGetConsumptionProfile(string itemId, out UnderworldConsumptionProfile profile)
        {
            profile = null;
            return Products.TryGetValue(itemId, out UnderworldProductDefinition definition)
                && (profile = definition.ConsumptionProfile) != null;
        }

        internal static bool IsContraband(string itemId)
        {
            return Products.TryGetValue(itemId, out UnderworldProductDefinition definition) && definition.Sampleable;
        }

        internal static int GetProductMarker(string itemId)
        {
            return ProductMarkers.TryGetValue(itemId ?? string.Empty, out int marker) ? marker : 0;
        }

        internal static string ResolveProductMarker(int marker)
        {
            return MarkerProducts.TryGetValue(marker, out string itemId) ? itemId : string.Empty;
        }

        internal static IEnumerable<UnderworldProductDefinition> GetSampleableProducts()
        {
            foreach (UnderworldProductDefinition product in Products.Values)
            {
                if (product.Sampleable)
                {
                    yield return product;
                }
            }
        }

        private static UnderworldProductDefinition CreateProduct(
            string itemId,
            string displayName,
            int basePrice,
            int potencyTier,
            int addictionDelta,
            int basePotency,
            int baseToxicity,
            int baseTraceability,
            bool sampleable = false,
            bool finishedProduct = false,
            UnderworldConsumptionProfile consumptionProfile = null)
        {
            return new UnderworldProductDefinition
            {
                ItemId = itemId,
                DisplayName = displayName,
                BasePrice = basePrice,
                PotencyTier = potencyTier,
                AddictionDelta = addictionDelta,
                BasePotency = basePotency,
                BaseToxicity = baseToxicity,
                BaseTraceability = baseTraceability,
                Sampleable = sampleable,
                FinishedProduct = finishedProduct,
                ConsumptionProfile = consumptionProfile,
            };
        }

        private static UnderworldConsumptionProfile CreateConsumptionProfile(
            string itemId,
            string conditionId,
            int basePotency,
            int baseToxicity,
            int baseDuration,
            params UnderworldConsumptionRoute[] allowedRoutes)
        {
            return CreateConsumptionProfile(itemId, conditionId, basePotency, baseToxicity, baseDuration, null, null, false, allowedRoutes);
        }

        private static UnderworldConsumptionProfile CreateConsumptionProfile(
            string itemId,
            string conditionId,
            int basePotency,
            int baseToxicity,
            int baseDuration,
            string crashConditionId = null,
            string immediateHookId = null,
            bool applyVanillaSmoking = false,
            params UnderworldConsumptionRoute[] allowedRoutes)
        {
            return new UnderworldConsumptionProfile(
                itemId,
                conditionId,
                basePotency,
                baseToxicity,
                baseDuration,
                allowedRoutes,
                crashConditionId,
                immediateHookId,
                applyVanillaSmoking);
        }
    }
}
