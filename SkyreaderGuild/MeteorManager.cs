namespace SkyreaderGuild
{
    /// <summary>
    /// Handles meteor zone spawning on the overworld using Region.CreateRandomSite.
    /// Called daily from the GameDate.AdvanceDay Harmony postfix.
    /// </summary>
    public static class MeteorManager
    {
        public const int BASE_SPAWN_CHANCE_PERCENT = 15;
        public const int RESEARCHER_BONUS_PERCENT = 5;
        public const int BASE_SPAWN_RADIUS = 12;

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

            Region region = EClass.world.region;
            if (region == null)
            {
                SkyreaderGuild.Log("Meteor spawn failed: world region is null.");
                return false;
            }

            Zone centerZone = EClass._zone?.GetTopZone() ?? region;
            if (centerZone == region)
            {
                SkyreaderGuild.Log("Meteor spawn using world region as center because current top zone is null.");
            }

            int radius = GetSpawnRadius(quest);
            region.InitElomap();
            Point centerPoint = new Point(
                centerZone.IsRegion ? EClass.pc.pos.x + EClass.scene.elomap.minX : centerZone.x,
                centerZone.IsRegion ? EClass.pc.pos.z + EClass.scene.elomap.minY : centerZone.y);
            Point spawnPoint = region.GetRandomPoint(centerPoint.x, centerPoint.z, radius);

            if (spawnPoint == null)
            {
                SkyreaderGuild.Log("Meteor spawn failed: no valid land location found.");
                return false;
            }

            Zone zone = SpatialGen.Create("srg_meteor", region, register: true, spawnPoint.x, spawnPoint.z) as Zone;
            if (zone == null)
            {
                SkyreaderGuild.Log("Meteor spawn failed: SpatialGen returned null.");
                return false;
            }

            zone._dangerLv = 1;
            zone.isRandomSite = true;
            zone.dateExpire = EClass.world.date.GetRaw() + 10080;
            if (region.elomap.IsSnow(zone.x, zone.y))
            {
                zone.icon++;
            }
            region.elomap.SetZone(zone.x, zone.y, zone);
            object objmap = region.elomap.GetType().GetField("objmap")?.GetValue(region.elomap);
            objmap?.GetType().GetMethod("UpdateMeshImmediate")?.Invoke(objmap, null);

            Msg.SayRaw("A streak of light crosses the sky! A meteor has fallen nearby.");
            SkyreaderGuild.Log($"Spawned meteor site at ({zone.x},{zone.y}), DangerLv={zone.DangerLv}, radius={radius}.");
            return true;
        }

        private static int GetSpawnRadius(QuestSkyreader quest)
        {
            switch (quest.GetCurrentRank())
            {
                case GuildRank.PrincipalStarseeker: return 4;
                case GuildRank.Understander: return 6;
                case GuildRank.CosmosApplied: return 8;
                case GuildRank.CosmosAddled: return 8;
                case GuildRank.Researcher: return 10;
                default: return BASE_SPAWN_RADIUS;
            }
        }
    }
}
