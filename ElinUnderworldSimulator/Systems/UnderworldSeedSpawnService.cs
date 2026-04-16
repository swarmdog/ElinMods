using System;
using System.Collections.Generic;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldSeedSpawnService
    {
        private sealed class RegionalSeedCandidate
        {
            internal string CropId;
            internal int Chance;
        }

        internal static void TrySpawnRegionalSeed(Zone zone)
        {
            if (zone?.map == null)
            {
                return;
            }

            List<RegionalSeedCandidate> candidates = BuildCandidates(zone);
            if (candidates.Count == 0)
            {
                return;
            }

            RegionalSeedCandidate selected = candidates.Count == 1 ? candidates[0] : candidates[EClass.rnd(candidates.Count)];
            if (selected == null || EClass.rnd(100) >= selected.Chance)
            {
                return;
            }

            if (HasMatchingSeed(zone, selected.CropId))
            {
                return;
            }

            Point spawnPoint = FindSpawnPoint(zone);
            if (spawnPoint == null || !spawnPoint.IsValid)
            {
                return;
            }

            zone.AddCard(TraitSeed.MakeSeed(selected.CropId), spawnPoint);
        }

        private static List<RegionalSeedCandidate> BuildCandidates(Zone zone)
        {
            List<RegionalSeedCandidate> candidates = new List<RegionalSeedCandidate>();
            bool isWinter = EClass.world?.date?.month == 12;
            if (zone is Zone_Noyel || isWinter)
            {
                candidates.Add(new RegionalSeedCandidate
                {
                    CropId = UnderworldContentIds.CropFrostbloomId,
                    Chance = UnderworldConfig.FrostbloomSeedChance.Value,
                });
            }

            if (zone is Zone_Lothria || zone.ParentZone is Zone_Lothria)
            {
                candidates.Add(new RegionalSeedCandidate
                {
                    CropId = UnderworldContentIds.CropAshveilId,
                    Chance = UnderworldConfig.AshveilSeedChance.Value,
                });
            }

            return candidates;
        }

        private static bool HasMatchingSeed(Zone zone, string cropId)
        {
            SourceObj.Row cropRow = EClass.sources.objs.alias.TryGetValue(cropId);
            if (cropRow == null)
            {
                throw new InvalidOperationException($"Unknown regional seed crop '{cropId}'.");
            }

            foreach (Thing thing in zone.map.things)
            {
                if (thing != null && thing.id == "seed" && thing.refVal == cropRow.id)
                {
                    return true;
                }
            }

            return false;
        }

        private static Point FindSpawnPoint(Zone zone)
        {
            for (int i = 0; i < 24; i++)
            {
                Point point = zone.bounds.GetRandomSurface(centered: false, walkable: true, allowWater: false);
                if (IsValidSpawnPoint(point))
                {
                    return point;
                }
            }

            for (int i = 0; i < 24; i++)
            {
                Point point = zone.bounds.GetRandomPoint()?.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false);
                if (IsValidSpawnPoint(point))
                {
                    return point;
                }
            }

            return null;
        }

        private static bool IsValidSpawnPoint(Point point)
        {
            if (point == null || !point.IsValid || point.IsBlocked || point.HasChara || point.HasObj)
            {
                return false;
            }

            return point.cell != null && !point.cell.HasLiquid;
        }
    }
}
