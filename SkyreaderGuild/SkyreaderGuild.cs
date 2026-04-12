using System;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;

namespace SkyreaderGuild {

    public enum GuildRank
    {
        Wanderer = 0,
        Seeker = 200,
        Researcher = 500,
        CosmosAddled = 1500,
        CosmosApplied = 3000,
        Understander = 5000,
        PrincipalStarseeker = 10000
    }

    internal static class ModInfo
    {
        internal const string Guid = "mistermeagle.elin.skyreaderguild";
        internal const string Name = "The Skyreader's Guild";
        internal const string Version = "1.1.0";
    }

    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    internal class SkyreaderGuild : BaseUnityPlugin
    {
        /// <summary>
        /// Set to false to suppress all debug logging.
        /// </summary>
        internal static bool DebugLogging = true;
        internal static ManualLogSource LogSource;

        // Register custom data that is not represented in source sheets yet.
        public void OnStartCore()
        {
            var s = Core.Instance.sources;
            this.AddQuest(s);
            this.RegisterMeteorZone(s);
        }

        private void AddQuest(SourceManager sources)
        {
            //Sets up the quest's information so it can be instantiated later
            var quest = sources.quests.CreateRow();
            quest.id = "skyreader_guild";
            quest.name = "the skyreader's guild";
            quest.name_JP = "..."; //TODO
            quest.type = "SkyreaderGuild.QuestSkyreader";
            //The message on the quest board.
            //For multistep quests, each step's description is separated by pipes.
            quest.detail =
                "#pc, you've joined the astrological society.  This will be the status display.";
            quest.detail_JP =
                "#pc, ..."; //TODO
            quest.drama = new string[] { "skyreader_guild", "main" };
            sources.quests.rows.Add(quest);

        }

        /// <summary>
        /// Registers the srg_meteor zone type in SourceZone.
        /// </summary>
        private void RegisterMeteorZone(SourceManager sources)
        {
            var zone = new SourceZone.Row();
            zone.id = "srg_meteor";
            zone.parent = "";
            zone.name_JP = "隕石の衝突地点";
            zone.name = "Meteor Impact Site";
            zone.type = "Zone_Field";
            zone.LV = 0;
            zone.chance = 0;
            zone.faction = "";
            zone.value = 0;
            zone.idProfile = "Lesimas";
            zone.idFile = new string[0];
            zone.idBiome = "Plain";
            zone.idGen = "";
            zone.idPlaylist = "Field";
            zone.tag = new string[0]; // NOT "random" — we control spawning
            zone.cost = 0;
            zone.dev = 0;
            zone.image = "";
            zone.pos = new int[] { 0, 0, 389 };
            zone.questTag = new string[0];
            zone.textFlavor_JP = "星の破片が散乱している。";
            zone.textFlavor = "Fragments of a fallen star litter the ground.";
            zone.detail_JP = "";
            zone.detail = "";

            sources.zones.rows.Add(zone);
            sources.zones.map[zone.id] = zone;
            Log("Registered srg_meteor zone type.");
        }

        private void Awake()
        {
            LogSource = Logger;
            Harmony harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();
            Log("Harmony patches installed.");
        }

        internal static void Log(string payload)
        {
            if (!DebugLogging) return;
            if (LogSource != null)
            {
                LogSource.LogInfo(payload);
            }
            else
            {
                Console.WriteLine("[SkyreaderGuild]" + payload);
            }
        }
    }

    // ─── Phase 1 Patches ────────────────────────────────────────────────

    // Starchart interaction — initiates guild membership
    [HarmonyPatch(typeof(TraitScrollMap), "OnRead")]
    public static class YithInvitePatch
    {
        public static bool Prefix(ref TraitScrollMap __instance)
        {
            
            if(__instance.owner.id == "srg_starchart")
            {
                Chara c = EClass.game.cards.globalCharas.Find("srg_arkyn");
                if(c == null)
                {
                    SkyreaderGuild.Log("spawning arkyn");
                    c = CharaGen.Create("srg_arkyn", -1);
                    c.SetGlobal();
                    EClass.game.spatials.Find("somewhere")?.AddCard(c);
                    if (c.currentZone == null)
                    {
                        SkyreaderGuild.Log("Warning: 'somewhere' zone not found; Arkyn created but not placed.");
                    }
                }
                else
                {
                    SkyreaderGuild.Log("Arkyn exists in the world and all is well.");
                }

                if(!EClass.game.quests.IsCompleted("skyreader_guild") &&
                    !EClass.game.quests.IsStarted<QuestSkyreader>())
                {
                    SkyreaderGuild.Log("Initiating player into skyreader guild...");
                    EClass.game.quests.globalList.Add(Quest.Create("skyreader_guild").SetClient(c, false));
                    Msg.SayRaw("Your head pounds as the sigils on the chart waver.  After a period of time, you discover the scroll is a lost history of the Skyreader's Guild, who tracked the occasional falling meteorite.  Keep an eye out.");
                    EClass.game.quests.Start("skyreader_guild", c, true);
                }
                else
                {
                    Msg.SayRaw("The starchart dissolves into celestial dust. Its mysteries are already known to you.");
                }

                __instance.owner.ModNum(-1, true);
                return false;
            }
            return true;
            
        }  
        
    }

    // Drop the starchart when we kill a yith growth
    [HarmonyPatch(typeof(Card), "SpawnLoot")]
    public static class YithDropStarchart
    {
        public static void Postfix(ref Card __instance)
        {
            if (!__instance.isChara) return;
            SkyreaderGuild.Log("Checking " + __instance.Chara.id.ToString() + " to see if its srg_growth...");
            if(__instance.Chara.id == "srg_growth" &&
               !EClass.game.quests.IsCompleted("skyreader_guild") &&
                    !EClass.game.quests.IsStarted<QuestSkyreader>() /*&&
                    EClass.rnd(100) < 21*/) // approx 1 in 5 chance to drop
            {
                Point nearestPoint = __instance.GetRootCard().pos;
                if (nearestPoint.IsBlocked)
                {
                    nearestPoint = nearestPoint.GetNearestPoint(false, true, true, false);
                }
                SkyreaderGuild.Log("spawning starchart");
                __instance.Chara.currentZone.AddThing("srg_starchart", nearestPoint);
            }
        }
    }

    // Spawn our yith growth in appropriate Nefia zones
    [HarmonyPatch(typeof(Zone), "OnVisit")]
    public static class SpawnYithGrowthOnVisit
    {
        public static void Postfix(Zone __instance)
        {
            // Only attempt spawn in Nefia zones at DangerLv 15+
            if (!__instance.IsNefia) return;
            if (__instance.DangerLv < 15) return;
            if (EClass.game.quests.IsCompleted("skyreader_guild") ||
                    EClass.game.quests.IsStarted<QuestSkyreader>()) return;

            SkyreaderGuild.Log($"Nefia DangerLv={__instance.DangerLv}, rolling for Yith spawn");
            if (EClass.rnd(100) >= 20) return; // 20% chance to spawn one

            // Ensure the specific Chara does not already exist in the zone
            Chara existingChara = __instance.FindChara("srg_growth");
            if (existingChara != null) { SkyreaderGuild.Log("existing growth found!"); return; }

            // Spawn the specific Chara
            Point spawnPoint = __instance.GetRandomVisitPos(EClass.pc);
            var newChara = __instance.AddChara("srg_growth", spawnPoint.x, spawnPoint.z);
            SkyreaderGuild.Log($"Spawned specific Chara {newChara.id} in Nefia {__instance.id} at {spawnPoint.x} / {spawnPoint.z} - (Level {__instance.DangerLv}).");

        }
    }

    // ─── Phase 2 Patches ────────────────────────────────────────────────

    // Trigger meteor spawn check on each new day
    [HarmonyPatch(typeof(GameDate), "AdvanceDay")]
    public static class MeteorSpawnOnDayAdvance
    {
        public static void Postfix()
        {
            MeteorManager.TrySpawnMeteor();
        }
    }

    // Override map generation for our meteor zone
    [HarmonyPatch(typeof(Zone_Field), "OnGenerateMap")]
    public static class MeteorZoneGenerationPatch
    {
        public static void Postfix(Zone_Field __instance)
        {
            if (__instance.id != "srg_meteor") return;

            SkyreaderGuild.Log("Generating meteor impact site map...");
            PopulateMeteorZone(__instance);
        }

        private static void PopulateMeteorZone(Zone zone)
        {
            var map = EClass._map;

            // 1. Place the Meteor Core near the center, but only on a visible empty surface.
            Point center = map.bounds.GetCenterPos();
            Point corePoint = center.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false)
                ?? map.bounds.GetRandomSurface(center.x, center.z, 8);
            Card meteorCore = zone.AddThing("srg_meteor_core", corePoint);
            SkyreaderGuild.Log($"Placed meteor core id={meteorCore.id}, name={meteorCore.Name}, trait={meteorCore.trait.GetType().Name} at ({corePoint.x},{corePoint.z})");

            // 2. Place 3-5 debris objects around the core
            int debrisCount = 3 + EClass.rnd(3);
            for (int i = 0; i < debrisCount; i++)
            {
                Point p = map.bounds.GetRandomSurface(center.x, center.z, 6);
                if (p != null && !p.HasBlock && !p.HasThing)
                {
                    zone.AddThing("srg_debris", p);
                }
            }

            // 3. Place 3-5 junk loot items
            int lootCount = 3 + EClass.rnd(3);
            for (int i = 0; i < lootCount; i++)
            {
                Point p = map.bounds.GetRandomSurface(center.x, center.z, 8);
                if (p != null && !p.HasBlock)
                {
                    Thing t = ThingGen.CreateFromCategory("junk", zone.DangerLv);
                    zone.AddCard(t, p);
                }
            }

            // 4. Possibly place raw ores (30% chance each, up to 3)
            for (int i = 0; i < 3; i++)
            {
                if (EClass.rnd(100) < 30)
                {
                    Point p = map.bounds.GetRandomSurface(center.x, center.z, 8);
                    if (p != null && !p.HasBlock)
                    {
                        Thing ore = ThingGen.CreateFromCategory("ore", zone.DangerLv);
                        zone.AddCard(ore, p);
                    }
                }
            }

            // 5. Spawn hostile creatures around the impact
            int mobCount = 2 + EClass.rnd(3);
            for (int i = 0; i < mobCount; i++)
            {
                zone.SpawnMob();
            }

            SkyreaderGuild.Log($"Meteor zone populated: {debrisCount} debris, {lootCount} junk, {mobCount} mobs");
        }
    }

    // ─── Quest Class ────────────────────────────────────────────────────

    public class QuestSkyreader : QuestSequence
    {

        public override void OnInit()
        {
            
            this.id = "skyreader_guild";
            this.gp = this.meteors_found = this.touched_cleansed = 0;
        }

        public override string TitlePrefix { get { return "⚆"; } }
        public override bool UpdateOnTalk()
        {
            return false;
        }

        public GuildRank GetCurrentRank()
        {
            if (gp >= (int)GuildRank.PrincipalStarseeker) return GuildRank.PrincipalStarseeker;
            if (gp >= (int)GuildRank.Understander) return GuildRank.Understander;
            if (gp >= (int)GuildRank.CosmosApplied) return GuildRank.CosmosApplied;
            if (gp >= (int)GuildRank.CosmosAddled) return GuildRank.CosmosAddled;
            if (gp >= (int)GuildRank.Researcher) return GuildRank.Researcher;
            if (gp >= (int)GuildRank.Seeker) return GuildRank.Seeker;
            return GuildRank.Wanderer;
        }

        public GuildRank? GetNextRank()
        {
            var current = GetCurrentRank();
            var values = (GuildRank[])Enum.GetValues(typeof(GuildRank));
            int idx = Array.IndexOf(values, current);
            if (idx >= 0 && idx < values.Length - 1)
                return values[idx + 1];
            return null;
        }

        /// <summary>
        /// Awards guild points and notifies the player on rank-up.
        /// </summary>
        public void AddGuildPoints(int amount)
        {
            int oldRank = (int)GetCurrentRank();
            gp += amount;
            var newRank = GetCurrentRank();
            if ((int)newRank > oldRank)
            {
                Msg.SayRaw($"Your standing in the Skyreader's Guild has risen to {newRank}!");
            }
            SkyreaderGuild.Log($"Added {amount} GP. Total: {gp}, Rank: {newRank}");
        }

        public override string GetDetailText(bool onJournal = false)
        {
            string text = base.GetDetailText(onJournal);
            var rank = GetCurrentRank();
            var nextRank = GetNextRank();

            text += "\n\n";
            text += "Skyreader's Guild".TagColor(FontColor.Topic) + "\n";
            text += $"Rank: {rank}\n";
            text += $"Guild Points: {gp}\n";

            if (nextRank != null)
            {
                int needed = (int)nextRank.Value - gp;
                text += $"Next Rank: {nextRank.Value} ({needed} GP needed)\n";
            }

            text += "\n";
            text += "Progress".TagColor(FontColor.Topic) + "\n";
            text += $"Meteors Found: {meteors_found}\n";
            text += $"Touched Cleansed: {touched_cleansed}\n";

            return text;
        }

        public override string GetTrackerText()
        {
            return $"⚆ {GetCurrentRank()} — {gp} GP";
        }


        [JsonProperty]
        public int gp;

        [JsonProperty]
        public int meteors_found;

        [JsonProperty]
        public int touched_cleansed;

    }

    

}
