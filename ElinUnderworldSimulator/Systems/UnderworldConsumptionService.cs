using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElinUnderworldSimulator
{
    internal enum UnderworldConsumptionRoute
    {
        Drink,
        Smoke,
        Eat,
        Blend,
        Throw,
    }

    internal readonly struct UnderworldConsumptionContext
    {
        internal UnderworldConsumptionContext(
            Chara user,
            Thing item,
            UnderworldConsumptionRoute route,
            Card target = null,
            Point groundPoint = null)
        {
            User = user;
            Item = item;
            Route = route;
            Target = target;
            GroundPoint = groundPoint;
        }

        internal Chara User { get; }

        internal Thing Item { get; }

        internal UnderworldConsumptionRoute Route { get; }

        internal Card Target { get; }

        internal Point GroundPoint { get; }
    }

    internal sealed class UnderworldConsumptionProfile
    {
        internal UnderworldConsumptionProfile(
            string itemId,
            string primaryConditionId,
            int basePotency,
            int baseToxicity,
            int baseDuration,
            IEnumerable<UnderworldConsumptionRoute> allowedRoutes,
            string crashConditionId = null,
            string immediateHookId = null,
            bool applyVanillaSmoking = false)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            PrimaryConditionId = primaryConditionId ?? string.Empty;
            BasePotency = basePotency;
            BaseToxicity = baseToxicity;
            BaseDuration = baseDuration;
            CrashConditionId = crashConditionId ?? string.Empty;
            ImmediateHookId = immediateHookId ?? string.Empty;
            ApplyVanillaSmoking = applyVanillaSmoking;
            AllowedRoutes = new HashSet<UnderworldConsumptionRoute>(allowedRoutes ?? Array.Empty<UnderworldConsumptionRoute>());
        }

        internal string ItemId { get; }

        internal string PrimaryConditionId { get; }

        internal int BasePotency { get; }

        internal int BaseToxicity { get; }

        internal int BaseDuration { get; }

        internal IReadOnlyCollection<UnderworldConsumptionRoute> AllowedRoutes { get; }

        internal string CrashConditionId { get; }

        internal string ImmediateHookId { get; }

        internal bool ApplyVanillaSmoking { get; }

        internal bool AllowsRoute(UnderworldConsumptionRoute route)
        {
            foreach (UnderworldConsumptionRoute allowedRoute in AllowedRoutes)
            {
                if (allowedRoute == route)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal readonly struct UnderworldConsumptionResult
    {
        internal UnderworldConsumptionResult(
            string itemId,
            UnderworldConsumptionRoute route,
            int potency,
            int toxicity,
            int traceability,
            int duration,
            int conditionPower,
            float statScale,
            IReadOnlyList<string> appliedConditionIds)
        {
            ItemId = itemId ?? string.Empty;
            Route = route;
            Potency = potency;
            Toxicity = toxicity;
            Traceability = traceability;
            Duration = duration;
            ConditionPower = conditionPower;
            StatScale = statScale;
            AppliedConditionIds = appliedConditionIds ?? Array.Empty<string>();
        }

        internal string ItemId { get; }

        internal UnderworldConsumptionRoute Route { get; }

        internal int Potency { get; }

        internal int Toxicity { get; }

        internal int Traceability { get; }

        internal int Duration { get; }

        internal int ConditionPower { get; }

        internal float StatScale { get; }

        internal IReadOnlyList<string> AppliedConditionIds { get; }
    }

    internal static class UnderworldConsumptionService
    {
        internal const string DreamBlendHookId = "dream_blend";
        internal const string AshveilCloudHookId = "ashveil_cloud";

        private static readonly HashSet<string> MissingMetricWarnings = new HashSet<string>(StringComparer.Ordinal);

        internal static UnderworldConsumptionResult Apply(in UnderworldConsumptionContext context)
        {
            if (context.User == null)
            {
                throw new InvalidOperationException("Underworld consumption requires a valid user.");
            }

            if (context.Item == null)
            {
                throw new InvalidOperationException("Underworld consumption requires a valid item.");
            }

            if (!UnderworldDrugCatalog.TryGetConsumptionProfile(context.Item.id, out UnderworldConsumptionProfile profile))
            {
                throw new InvalidOperationException($"Underworld item '{context.Item.id}' is missing a consumption profile.");
            }

            if (!profile.AllowsRoute(context.Route))
            {
                throw new InvalidOperationException($"Underworld item '{context.Item.id}' does not support route '{context.Route}'.");
            }

            return context.Route switch
            {
                UnderworldConsumptionRoute.Blend => ApplyBlend(context, profile),
                UnderworldConsumptionRoute.Throw => ApplyThrow(context, profile),
                _ => ApplyConditionConsumption(context, profile, registerDrugUse: context.Route != UnderworldConsumptionRoute.Throw),
            };
        }

        internal static bool TryApplyFoodTraits(Chara user, Card consumedCard)
        {
            if (user == null || consumedCard is not Thing consumedThing)
            {
                return false;
            }

            bool handled = false;
            List<string> applied = null;

            int dreamPower = consumedThing.Evalue(UnderworldContentIds.DreamTraitElement);
            if (dreamPower > 0 && UnderworldDrugCatalog.TryGetConsumptionProfile(UnderworldContentIds.PowderDreamId, out UnderworldConsumptionProfile dreamProfile))
            {
                UnderworldConsumptionResult result = ApplyTraitDrivenCondition(
                    user,
                    consumedThing,
                    dreamProfile,
                    UnderworldContentIds.ConDreamHigh,
                    durationMultiplier: 1.5f);
                handled |= result.AppliedConditionIds.Count > 0;
                if (handled)
                {
                    applied ??= new List<string>();
                    applied.AddRange(result.AppliedConditionIds);
                }
            }

            int voidPower = consumedThing.Evalue(UnderworldContentIds.VoidTraitElement);
            if (voidPower > 0 && UnderworldDrugCatalog.TryGetConsumptionProfile(UnderworldContentIds.SaltsVoidId, out UnderworldConsumptionProfile voidProfile))
            {
                UnderworldConsumptionResult result = ApplyTraitDrivenCondition(
                    user,
                    consumedThing,
                    voidProfile,
                    UnderworldContentIds.ConVoidRage);
                handled |= result.AppliedConditionIds.Count > 0;
                if (result.AppliedConditionIds.Count > 0)
                {
                    applied ??= new List<string>();
                    applied.AddRange(result.AppliedConditionIds);
                }
            }

            if (handled && user.IsPC)
            {
                UnderworldRuntime.RegisterDrugUse(consumedThing);
                UnderworldPlugin.Log(
                    $"Underworld consume {consumedThing.id} route=Eat potency={consumedThing.Evalue(UnderworldContentIds.PotencyElement)} applied=[{(applied != null ? string.Join(",", applied) : string.Empty)}]");
            }

            return handled;
        }

        private static UnderworldConsumptionResult ApplyConditionConsumption(
            in UnderworldConsumptionContext context,
            UnderworldConsumptionProfile profile,
            bool registerDrugUse)
        {
            return ApplyConditionCore(
                context.User,
                context.Item,
                profile,
                profile.PrimaryConditionId,
                context.Route,
                registerDrugUse,
                durationMultiplier: 1f,
                applyVanillaSmoking: context.Route == UnderworldConsumptionRoute.Smoke && profile.ApplyVanillaSmoking);
        }

        private static UnderworldConsumptionResult ApplyTraitDrivenCondition(
            Chara user,
            Thing item,
            UnderworldConsumptionProfile profile,
            string conditionId,
            float durationMultiplier = 1f)
        {
            return ApplyConditionCore(
                user,
                item,
                profile,
                conditionId,
                UnderworldConsumptionRoute.Eat,
                registerDrugUse: false,
                durationMultiplier: durationMultiplier,
                applyVanillaSmoking: false);
        }

        private static UnderworldConsumptionResult ApplyConditionCore(
            Chara user,
            Thing item,
            UnderworldConsumptionProfile profile,
            string conditionId,
            UnderworldConsumptionRoute route,
            bool registerDrugUse,
            float durationMultiplier,
            bool applyVanillaSmoking)
        {
            if (string.IsNullOrEmpty(conditionId))
            {
                throw new InvalidOperationException($"Underworld item '{item.id}' route '{route}' is missing a condition id.");
            }

            int potency = ResolveMetric(item, UnderworldContentIds.PotencyElement, profile.BasePotency, "potency");
            int toxicity = ResolveMetric(item, UnderworldContentIds.ToxicityElement, profile.BaseToxicity, "toxicity");
            int traceability = ResolveMetric(item, UnderworldContentIds.TraceabilityElement, ResolveTraceabilityFallback(item.id), "traceability");

            float potencyScale = Mathf.Max(0.1f, potency / (float)Math.Max(1, profile.BasePotency));
            float statScale = potencyScale * UnderworldConfig.DrugStatBonusMultiplier.Value;
            int duration = Mathf.Max(
                1,
                Mathf.RoundToInt(profile.BaseDuration * potencyScale * UnderworldConfig.DrugDurationMultiplier.Value * durationMultiplier));
            int conditionPower = Mathf.Max(1, Mathf.RoundToInt(statScale * 100f));

            UnderworldRuntime.ClearCrashConditionForProduct(item);

            List<string> applied = new List<string>();
            Condition added = user.AddCondition(
                Condition.Create(conditionId, conditionPower, con =>
                {
                    con.refVal = duration;
                    con.refVal2 = potency;
                }));
            if (added != null)
            {
                applied.Add(conditionId);
            }

            if (applyVanillaSmoking)
            {
                Condition smoking = user.AddCondition<ConSmoking>(Mathf.Max(30, duration / 2));
                if (smoking != null || user.HasCondition<ConSmoking>())
                {
                    applied.Add(nameof(ConSmoking));
                }
            }

            if (registerDrugUse && user.IsPC)
            {
                UnderworldRuntime.RegisterDrugUse(item);
            }

            UnderworldPlugin.Log(
                $"Underworld consume {item.id} route={route} potency={potency} toxicity={toxicity} trace={traceability} duration={duration} statScale={statScale:F2} applied=[{string.Join(",", applied)}]");

            return new UnderworldConsumptionResult(
                item.id,
                route,
                potency,
                toxicity,
                traceability,
                duration,
                conditionPower,
                statScale,
                applied);
        }

        private static UnderworldConsumptionResult ApplyBlend(
            in UnderworldConsumptionContext context,
            UnderworldConsumptionProfile profile)
        {
            if (!string.Equals(profile.ImmediateHookId, DreamBlendHookId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Blend route for '{context.Item.id}' is missing the expected dream blend hook.");
            }

            if (context.Target is not Thing targetThing)
            {
                throw new InvalidOperationException("Dream Powder blend requires a target food item.");
            }

            int potency = ResolveMetric(context.Item, UnderworldContentIds.PotencyElement, profile.BasePotency, "potency");
            int toxicity = ResolveMetric(context.Item, UnderworldContentIds.ToxicityElement, profile.BaseToxicity, "toxicity");
            int traceability = ResolveMetric(context.Item, UnderworldContentIds.TraceabilityElement, ResolveTraceabilityFallback(context.Item.id), "traceability");

            Thing blendedServing = targetThing.Split(1);
            int dreamTraitPower = Mathf.Max(1, Mathf.RoundToInt(potency / 10f));
            blendedServing.elements.SetBase(
                UnderworldContentIds.DreamTraitElement,
                Mathf.Max(blendedServing.Evalue(UnderworldContentIds.DreamTraitElement), dreamTraitPower));
            blendedServing.elements.SetBase(
                UnderworldContentIds.PotencyElement,
                Mathf.Max(blendedServing.Evalue(UnderworldContentIds.PotencyElement), potency));
            blendedServing.elements.SetBase(
                UnderworldContentIds.ToxicityElement,
                Mathf.Max(blendedServing.Evalue(UnderworldContentIds.ToxicityElement), toxicity));
            blendedServing.elements.SetBase(
                UnderworldContentIds.TraceabilityElement,
                Mathf.Max(blendedServing.Evalue(UnderworldContentIds.TraceabilityElement), traceability));

            context.User.Pick(blendedServing);
            context.Item.ModNum(-1);
            Msg.Say("blend_love", blendedServing);

            UnderworldPlugin.Log(
                $"Underworld blend {context.Item.id} -> {blendedServing.id} potency={potency} toxicity={toxicity} trace={traceability}");

            return new UnderworldConsumptionResult(
                context.Item.id,
                UnderworldConsumptionRoute.Blend,
                potency,
                toxicity,
                traceability,
                0,
                dreamTraitPower,
                dreamTraitPower / 10f,
                Array.Empty<string>());
        }

        private static UnderworldConsumptionResult ApplyThrow(
            in UnderworldConsumptionContext context,
            UnderworldConsumptionProfile profile)
        {
            if (!string.Equals(profile.ImmediateHookId, AshveilCloudHookId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Throw route for '{context.Item.id}' is missing the expected ashveil cloud hook.");
            }

            if (context.GroundPoint == null || !context.GroundPoint.IsValid)
            {
                throw new InvalidOperationException("Ashveil throw requires a valid target point.");
            }

            int potency = ResolveMetric(context.Item, UnderworldContentIds.PotencyElement, profile.BasePotency, "potency");
            int toxicity = ResolveMetric(context.Item, UnderworldContentIds.ToxicityElement, profile.BaseToxicity, "toxicity");
            int traceability = ResolveMetric(context.Item, UnderworldContentIds.TraceabilityElement, ResolveTraceabilityFallback(context.Item.id), "traceability");

            bool isHostileAct = context.User.IsPCParty;
            CellEffect cellEffect = new CellEffect
            {
                id = 4,
                idEffect = EffectId.Buff,
                power = potency,
                amount = 8,
                isHostileAct = isHostileAct,
                n1 = nameof(ConSeeInvisible),
                isBlessed = context.Item.IsBlessed,
                isCursed = context.Item.IsCursed,
                color = BaseTileMap.GetColorInt(ref context.Item.GetRandomColor(), context.Item.sourceRenderCard.colorMod),
            };

            EClass._map.SetLiquid(context.GroundPoint.x, context.GroundPoint.z, cellEffect);
            UnderworldPlugin.Log(
                $"Underworld throw {context.Item.id} point={context.GroundPoint.x},{context.GroundPoint.z} potency={potency}");

            return new UnderworldConsumptionResult(
                context.Item.id,
                UnderworldConsumptionRoute.Throw,
                potency,
                toxicity,
                traceability,
                0,
                potency,
                0f,
                new[] { nameof(ConSeeInvisible) });
        }

        private static int ResolveMetric(Thing item, int elementId, int fallbackValue, string metricName)
        {
            int explicitValue = item.Evalue(elementId);
            if (explicitValue > 0)
            {
                return explicitValue;
            }

            string key = item.id + ":" + metricName;
            if (MissingMetricWarnings.Add(key))
            {
                UnderworldPlugin.Warn(
                    $"Underworld item '{item.id}' is missing crafted {metricName}; falling back to catalog defaults once.");
            }

            return Mathf.Max(0, fallbackValue);
        }

        private static int ResolveTraceabilityFallback(string itemId)
        {
            return UnderworldDrugCatalog.TryGetProduct(itemId, out UnderworldProductDefinition definition)
                ? Mathf.Max(0, definition.BaseTraceability)
                : 0;
        }
    }
}
