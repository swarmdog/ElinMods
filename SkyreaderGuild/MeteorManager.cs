namespace SkyreaderGuild
{
    using UnityEngine;

    /// <summary>
    /// Handles meteor zone spawning on the overworld using Region.CreateRandomSite.
    /// Called daily from the GameDate.AdvanceDay Harmony postfix.
    /// </summary>
    public static class MeteorManager
    {
        public const int BASE_SPAWN_CHANCE_PERCENT = 15;
        public const int RESEARCHER_BONUS_PERCENT = 5;
        public const int BASE_SPAWN_RADIUS = 12;
        public const int SITE_EXPIRE_HOURS = 10080;
        public const int MIN_DANGER_LV = 5;
        public const int MAX_ACTIVE_METEOR_SITES = 2;
        public const int MAX_ACTIVE_RIFT_SITES = 1;

        public static readonly string[] meteorFallQuips = new string[]
        {
            "Great Scott!", "Christ on a crutch!"
        };

        /// <summary>
        /// Attempts to spawn a meteor impact site on the overworld near the player.
        /// Returns true if a meteor was successfully spawned.
        /// </summary>
        public static bool TrySpawnMeteor(bool force = false)
        {
            SkyreaderGuild.Log("Meteor spawn check started.");

            QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest == null)
            {
                SkyreaderGuild.Log("Meteor spawn skipped: Skyreader quest is not active.");
                return false;
            }

            if (CountSites("srg_meteor") >= MAX_ACTIVE_METEOR_SITES)
            {
                SkyreaderGuild.Log("Meteor spawn skipped: at active site cap.");
                return false;
            }

            int chance = BASE_SPAWN_CHANCE_PERCENT;
            if (quest.GetCurrentRank() >= GuildRank.Researcher)
            {
                chance += RESEARCHER_BONUS_PERCENT;
            }

            int roll = EClass.rnd(100);
            if (!force && roll >= chance)
            {
                SkyreaderGuild.Log($"Meteor spawn skipped: roll={roll}, chance={chance}.");
                return false;
            }
            if (force)
            {
                SkyreaderGuild.Log($"Meteor spawn forced for debug/testing. Natural roll={roll}, chance={chance}.");
            }

            Zone zone = SpawnSite("srg_meteor", GetSpawnRadius(quest), Mathf.Max(MIN_DANGER_LV, EClass.pc.LV / 2));
            if (zone == null)
            {
                return false;
            }
            string m = meteorFallQuips[EClass.rnd(meteorFallQuips.Length)];
            EClass.pc.TalkRaw(m);
            Msg.SayRaw("A streak of light crosses the sky! A meteor has fallen nearby.");
            SkyreaderGuild.Log($"Spawned meteor site at ({zone.x},{zone.y}), DangerLv={zone.DangerLv}.");
            return true;
        }

        /// <summary>
        /// Attempts to spawn an astral rift Nefia near the player.
        /// Returns the created rift zone, or null if the spawn failed.
        /// </summary>
        public static Zone TrySpawnAstralRift(bool force = false)
        {
            QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest == null)
            {
                SkyreaderGuild.Log("Astral rift spawn skipped: Skyreader quest is not active.");
                return null;
            }

            if (CountSites("srg_astral_rift") >= MAX_ACTIVE_RIFT_SITES)
            {
                SkyreaderGuild.Log("Astral rift spawn skipped: at active rift cap.");
                Msg.SayRaw("The cosmic energies dissipate. A rift already scars the land.");
                return null;
            }

            Zone zone = SpawnSite("srg_astral_rift", GetSpawnRadius(quest), Mathf.Max(MIN_DANGER_LV, EClass.pc.LV));
            if (zone == null)
            {
                if (!force)
                {
                    Msg.SayRaw("The cosmic energies dissipate before a rift can form.");
                }
                return null;
            }

            Msg.SayRaw("A wound of starlight tears open on the horizon.");
            SkyreaderGuild.Log($"Spawned astral rift at ({zone.x},{zone.y}), DangerLv={zone.DangerLv}.");
            return zone;
        }

        public static int CountSites(string zoneId)
        {
            int count = 0;
            Region region = EClass.world.region;
            if (region == null) return count;

            foreach (Spatial child in region.children)
            {
                Zone zone = child as Zone;
                if (zone != null && !zone.destryoed && zone.id == zoneId)
                {
                    count++;
                }
            }
            return count;
        }

        private static Zone SpawnSite(string zoneId, int radius, int dangerLv)
        {
            Region region = EClass.world.region;
            if (region == null)
            {
                SkyreaderGuild.Log($"{zoneId} spawn failed: world region is null.");
                return null;
            }

            Zone centerZone = EClass._zone?.GetTopZone() ?? region;
            if (centerZone == region)
            {
                SkyreaderGuild.Log($"{zoneId} spawn using world region as center because current top zone is null.");
            }

            region.InitElomap();
            Point centerPoint = new Point(
                centerZone.IsRegion ? EClass.pc.pos.x + EClass.scene.elomap.minX : centerZone.x,
                centerZone.IsRegion ? EClass.pc.pos.z + EClass.scene.elomap.minY : centerZone.y);
            Point spawnPoint = region.GetRandomPoint(centerPoint.x, centerPoint.z, radius);

            if (spawnPoint == null)
            {
                SkyreaderGuild.Log($"{zoneId} spawn failed: no valid land location found.");
                return null;
            }

            Zone zone = SpatialGen.Create(zoneId, region, register: true, spawnPoint.x, spawnPoint.z) as Zone;
            if (zone == null)
            {
                SkyreaderGuild.Log($"{zoneId} spawn failed: SpatialGen returned null.");
                return null;
            }

            zone._dangerLv = dangerLv;
            zone.isRandomSite = true;
            zone.dateExpire = EClass.world.date.GetRaw() + SITE_EXPIRE_HOURS;
            if (region.elomap.IsSnow(zone.x, zone.y))
            {
                zone.icon++;
            }
            region.elomap.SetZone(zone.x, zone.y, zone);
            object objmap = region.elomap.GetType().GetField("objmap")?.GetValue(region.elomap);
            objmap?.GetType().GetMethod("UpdateMeshImmediate")?.Invoke(objmap, null);

            return zone;
        }

        private static int GetSpawnRadius(QuestSkyreader quest)
        {
            return Mathf.Max(4, BASE_SPAWN_RADIUS - quest.GetMeteorSpawnDistanceReduction());
        }
    }
}
