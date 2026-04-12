using System;
using System.Collections.Generic;

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
            this.RegisterAstralRiftZone(s);
        }

        private void AddQuest(SourceManager sources)
        {
            //Sets up the quest's information so it can be instantiated later
            var quest = sources.quests.CreateRow();
            quest.id = "skyreader_guild";
            quest.name = "the skyreader's guild";
            quest.name_JP = "星読みのギルド";
            quest.type = "SkyreaderGuild.QuestSkyreader";
            //The message on the quest board.
            //For multistep quests, each step's description is separated by pipes.
            quest.detail =
                "#pc, you've joined the astrological society.  This will be the status display.";
            quest.detail_JP =
                "#pc、あなたは天文学会に入会しました。これがギルドの状況表示です。";
            quest.drama = new string[] { "skyreader_guild", "main" };
            sources.quests.rows.RemoveAll(row => row.id == quest.id);
            sources.quests.rows.Add(quest);
            sources.quests.map[quest.id] = quest;

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
            zone.pos = new int[] { 0, 0, 393 }; // ruin icon — meteor impact crater
            zone.questTag = new string[0];
            zone.textFlavor_JP = "星の破片が散乱している。";
            zone.textFlavor = "Fragments of a fallen star litter the ground.";
            zone.detail_JP = "";
            zone.detail = "";

            UpsertZone(sources, zone);
            Log("Registered srg_meteor zone type.");
        }

        /// <summary>
        /// Registers the srg_astral_rift zone type as a full vanilla Nefia.
        /// </summary>
        private void RegisterAstralRiftZone(SourceManager sources)
        {
            var zone = new SourceZone.Row();
            zone.id = "srg_astral_rift";
            zone.parent = "";
            zone.name_JP = "星霊の裂け目";
            zone.name = "Astral Rift";
            zone.type = "Zone_RandomDungeon";
            zone.LV = 0;
            zone.chance = 0;
            zone.faction = "";
            zone.value = 0;
            zone.idProfile = "Lesimas";
            zone.idFile = new string[0];
            zone.idBiome = "Ruin";
            zone.idGen = "";
            zone.idPlaylist = "Dungeon";
            zone.tag = new string[0];
            zone.cost = 0;
            zone.dev = 0;
            zone.image = "";
            zone.pos = new int[] { 0, 0, 395 }; // tower icon — astral rift energy column
            zone.questTag = new string[0];
            zone.textFlavor_JP = "次元の裂け目から異界のエネルギーが漏れ出ている。";
            zone.textFlavor = "Otherworldly energy bleeds from a dimensional rift.";
            zone.detail_JP = "";
            zone.detail = "";

            UpsertZone(sources, zone);
            Log("Registered srg_astral_rift zone type.");
        }

        private static void UpsertZone(SourceManager sources, SourceZone.Row zone)
        {
            sources.zones.rows.RemoveAll(row => row.id == zone.id);
            sources.zones.rows.Add(zone);
            sources.zones.map[zone.id] = zone;
        }

        private void Awake()
        {
            LogSource = Logger;
            ModUtil.RegisterSerializedTypeFallback("SkyreaderGuild", "SkyreaderGuild.QuestSkyreader", "QuestDummy");
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

    // Unified loot handler for Yith Growth starchart drop and boss kill rewards
    [HarmonyPatch(typeof(Card), "SpawnLoot")]
    public static class SpawnLootPatch
    {
        private static readonly HashSet<string> BossIds = new HashSet<string>
        {
            "srg_umbryon",
            "srg_solaris",
            "srg_erevor",
            "srg_quasarix",
        };

        public static void Postfix(ref Card __instance)
        {
            if (!__instance.isChara) return;
            Chara chara = __instance.Chara;
            if (chara == null) return;

            if (chara.id == "srg_growth")
                HandleYithGrowthDrop(__instance);
            else if (BossIds.Contains(chara.id))
                HandleBossKillReward(__instance, chara);
        }

        private static void HandleYithGrowthDrop(Card __instance)
        {
            SkyreaderGuild.Log("srg_growth killed, checking starchart drop...");
            if (EClass.game.quests.IsCompleted("skyreader_guild") ||
                EClass.game.quests.IsStarted<QuestSkyreader>()) return;
            // TODO: uncomment RNG check before release (currently 100% for testing)
            // if (EClass.rnd(100) >= 21) return; // approx 1 in 5 chance to drop

            Point nearestPoint = __instance.GetRootCard().pos;
            if (nearestPoint.IsBlocked)
            {
                nearestPoint = nearestPoint.GetNearestPoint(false, true, true, false);
            }
            SkyreaderGuild.Log("spawning starchart");
            __instance.Chara.currentZone.AddThing("srg_starchart", nearestPoint);
        }

        private static void HandleBossKillReward(Card __instance, Chara chara)
        {
            QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest != null)
            {
                int gpReward = chara.LV * 10;
                quest.AddGuildPoints(gpReward);
            }
            else
            {
                SkyreaderGuild.Log($"Boss reward GP skipped because QuestSkyreader is missing: {chara.id}");
            }

            int sourceCount = 2 + EClass.rnd(3);
            Point dropPoint = __instance.GetRootCard().pos;
            for (int i = 0; i < sourceCount; i++)
            {
                Point p = dropPoint.GetNearestPoint(allowBlock: false, allowChara: true, allowInstalled: true) ?? dropPoint;
                EClass._zone.AddThing("srg_meteorite_source", p);
            }

            Msg.SayRaw($"The cosmic energy of {chara.Name} dissipates, leaving fragments of meteoric ore.");
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

    // Arkyn visits civilized zones like a wandering adventurer
    [HarmonyPatch(typeof(Zone), "OnVisit")]
    public static class ArkynTownVisitPatch
    {
        public const int VISIT_CHANCE_PERCENT = 10;

        public static void Postfix(Zone __instance)
        {
            if (!(__instance is Zone_Civilized)) return;
            if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return;

            Chara arkyn = EClass.game.cards.globalCharas.Find("srg_arkyn");
            if (arkyn == null) return;
            if (arkyn.currentZone == __instance) return;

            // Don't move him if he's already visiting another zone
            if (arkyn.currentZone != null)
            {
                Zone current = arkyn.currentZone as Zone;
                if (current != null && current.id != "somewhere")
                    return;
            }

            if (EClass.rnd(100) >= VISIT_CHANCE_PERCENT) return;

            Point spawnPoint = __instance.GetRandomVisitPos(EClass.pc);
            if (spawnPoint == null || !spawnPoint.IsValid) return;

            __instance.AddCard(arkyn, spawnPoint);
            SkyreaderGuild.Log($"Arkyn appeared in {__instance.Name} at ({spawnPoint.x},{spawnPoint.z}).");
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

    // Reduce meteor site expiration when the player leaves after taking the core
    [HarmonyPatch(typeof(Zone), "Deactivate")]
    public static class MeteorDespawnOnCoreTaken
    {
        public static void Prefix(Zone __instance)
        {
            if (__instance.id != "srg_meteor") return;
            if (__instance.map == null) return;

            bool corePresent = false;
            foreach (Thing thing in __instance.map.things)
            {
                if (thing.id == "srg_meteor_core")
                {
                    corePresent = true;
                    break;
                }
            }

            if (!corePresent)
            {
                __instance.dateExpire = EClass.world.date.GetRaw() + 720; // 12 in-game hours
                SkyreaderGuild.Log($"Meteor site {__instance.uid} core taken — expire reduced to ~12h.");
            }
        }
    }

    // ─── Phase 4 Patches ─────────────────────────────────────────────────

    [HarmonyPatch(typeof(Zone), "OnVisit")]
    public static class TagMeteorTouchedOnCivilizedVisit
    {
        public const int MeteorTouchedKey = 9001;
        public const int MaxTouchedPerZone = 3;

        public static void Postfix(Zone __instance)
        {
            if (!(__instance is Zone_Civilized)) return;
            if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return;
            if (EClass._map == null) return;
            if (EClass.rnd(100) >= 30) return;

            int alreadyTouched = 0;
            var eligibleCharas = new List<Chara>();
            var eligibleThings = new List<Thing>();

            foreach (Chara chara in EClass._map.charas)
            {
                if (chara.GetInt(MeteorTouchedKey) > 0)
                {
                    alreadyTouched++;
                    continue;
                }
                if (IsEligibleTouchedChara(chara))
                {
                    eligibleCharas.Add(chara);
                }
            }

            foreach (Thing thing in EClass._map.things)
            {
                if (thing.GetInt(MeteorTouchedKey) > 0)
                {
                    alreadyTouched++;
                    continue;
                }
                if (IsEligibleTouchedThing(thing))
                {
                    eligibleThings.Add(thing);
                }
            }

            int remaining = MaxTouchedPerZone - alreadyTouched;
            if (remaining <= 0) return;

            int eligibleCount = eligibleCharas.Count + eligibleThings.Count;
            if (eligibleCount == 0) return;

            int count = Math.Min(1 + EClass.rnd(2), remaining);
            for (int i = 0; i < count && eligibleCount > 0; i++)
            {
                bool pickChara = eligibleCharas.Count > 0 && (eligibleThings.Count == 0 || EClass.rnd(eligibleCount) < eligibleCharas.Count);
                if (pickChara)
                {
                    Chara target = eligibleCharas.RandomItem();
                    target.SetInt(MeteorTouchedKey, 1);
                    eligibleCharas.Remove(target);
                    SkyreaderGuild.Log($"Tagged {target.Name} as Meteor Touched in {__instance.Name}.");
                }
                else
                {
                    Thing target = eligibleThings.RandomItem();
                    target.SetInt(MeteorTouchedKey, 1);
                    eligibleThings.Remove(target);
                    SkyreaderGuild.Log($"Tagged {target.Name} as Meteor Touched in {__instance.Name}.");
                }
                eligibleCount = eligibleCharas.Count + eligibleThings.Count;
            }
        }

        public static bool IsTouched(Card card)
        {
            return card != null && card.GetInt(MeteorTouchedKey) > 0;
        }

        private static bool IsEligibleTouchedChara(Chara chara)
        {
            if (chara == null || chara.IsPC || chara.isDead) return false;
            if (chara.IsGlobal || chara.IsPCFactionOrMinion) return false;
            if (chara.IsHostile()) return false;
            return true;
        }

        public static bool IsEligibleTouchedThing(Thing thing)
        {
            if (thing == null || thing.isDestroyed) return false;
            if (thing.c_isImportant || thing.isMasked || thing.isHidden) return false;
            if (thing.IsInstalled || !thing.trait.CanBeHeld) return false;
            if (thing.id == "srg_astral_portal") return false;
            return thing.GetValue() > 0;
        }
    }

    [HarmonyPatch(typeof(Zone_RandomDungeon), "OnGenerateMap")]
    public static class AstralRiftThemingPatch
    {
        public static void Postfix(Zone_RandomDungeon __instance)
        {
            if (__instance.id != "srg_astral_rift") return;

            int lootCount = 2 + EClass.rnd(3);
            int placedLoot = 0;
            for (int i = 0; i < lootCount; i++)
            {
                Point p = EClass._map.bounds.GetRandomSurface();
                if (p != null && !p.HasBlock && !p.HasThing)
                {
                    __instance.AddThing("srg_meteorite_source", p);
                    placedLoot++;
                }
            }

            int extraMobs = 1 + EClass.rnd(3);
            for (int i = 0; i < extraMobs; i++)
            {
                __instance.SpawnMob();
            }

            SkyreaderGuild.Log($"Astral rift floor themed: lv={__instance.lv}, meteorite={placedLoot}, extraMobs={extraMobs}.");
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
        private static readonly GuildRank[] RankOrder =
        {
            GuildRank.Wanderer,
            GuildRank.Seeker,
            GuildRank.Researcher,
            GuildRank.CosmosAddled,
            GuildRank.CosmosApplied,
            GuildRank.Understander,
            GuildRank.PrincipalStarseeker,
        };

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
            GuildRank current = GetCurrentRank();
            int idx = Array.IndexOf(RankOrder, current);
            if (idx >= 0 && idx < RankOrder.Length - 1)
            {
                return RankOrder[idx + 1];
            }
            return null;
        }

        /// <summary>
        /// Awards guild points and notifies the player on rank-up.
        /// </summary>
        public void AddGuildPoints(int amount)
        {
            if (amount <= 0)
            {
                SkyreaderGuild.Log($"Ignored non-positive GP award: {amount}");
                return;
            }

            GuildRank oldRank = GetCurrentRank();
            gp += amount;
            GuildRank newRank = GetCurrentRank();
            SkyreaderGuild.Log($"Added {amount} GP. Total: {gp}, Rank: {FormatRankName(newRank)}");
            if (newRank > oldRank)
            {
                OnRankUp(oldRank, newRank);
            }
        }

        private void OnRankUp(GuildRank oldRank, GuildRank newRank)
        {
            string msg = $"Your standing in the Skyreader's Guild has risen. You are now a {FormatRankName(newRank)}.";
            switch (newRank)
            {
                case GuildRank.Seeker:
                    msg += " You can now use Weave the Stars and Starforge at the Codex.";
                    break;
                case GuildRank.Researcher:
                    msg += " Meteor reports now land closer to your position.";
                    break;
                case GuildRank.CosmosAddled:
                    msg += " Deeper cosmic disturbances answer your study.";
                    break;
                case GuildRank.CosmosApplied:
                    msg += " You can now craft Ultima Projection boss scrolls.";
                    break;
                case GuildRank.Understander:
                    msg += " You can now craft the Astral Convergence Archivist scroll.";
                    break;
                case GuildRank.PrincipalStarseeker:
                    msg += " Meteor core item rewards are now doubled.";
                    break;
            }
            Msg.SayRaw(msg);
        }

        public static string FormatRankName(GuildRank rank)
        {
            switch (rank)
            {
                case GuildRank.Wanderer: return "Wanderer";
                case GuildRank.Seeker: return "Seeker";
                case GuildRank.Researcher: return "Researcher";
                case GuildRank.CosmosAddled: return "Cosmos-Addled";
                case GuildRank.CosmosApplied: return "Cosmos-Applied";
                case GuildRank.Understander: return "Understander";
                case GuildRank.PrincipalStarseeker: return "Principal Starseeker";
                default: throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unknown Skyreader guild rank.");
            }
        }

        public bool CanUseRecipe(string recipeId)
        {
            GuildRank rank = GetCurrentRank();
            switch (recipeId)
            {
                case "srg_weave_stars":
                case "srg_starforge":
                    return rank >= GuildRank.Seeker;
                case "srg_scroll_twilight":
                case "srg_scroll_radiance":
                case "srg_scroll_abyss":
                case "srg_scroll_nova":
                    return rank >= GuildRank.CosmosApplied;
                case "srg_scroll_convergence":
                    return rank >= GuildRank.Understander;
                default:
                    return true;
            }
        }

        public int GetMeteorSpawnDistanceReduction()
        {
            GuildRank rank = GetCurrentRank();
            if (rank >= GuildRank.Understander) return 8;
            if (rank >= GuildRank.CosmosApplied) return 6;
            if (rank >= GuildRank.CosmosAddled) return 4;
            if (rank >= GuildRank.Researcher) return 2;
            return 0;
        }

        public float GetMeteorRewardMultiplier()
        {
            return GetCurrentRank() >= GuildRank.PrincipalStarseeker ? 2f : 1f;
        }

        public override string GetDetailText(bool onJournal = false)
        {
            string text = base.GetDetailText(onJournal);
            GuildRank rank = GetCurrentRank();
            GuildRank? nextRank = GetNextRank();

            text += "\n\n";
            text += "Skyreader's Guild".TagColor(FontColor.Topic) + "\n";
            text += $"Rank: {FormatRankName(rank)}\n";

            if (nextRank != null)
            {
                int needed = (int)nextRank.Value - gp;
                text += $"Guild Points: {gp}/{(int)nextRank.Value} GP ({needed} to {FormatRankName(nextRank.Value)})\n";
            }
            else
            {
                text += $"Guild Points: {gp} GP (MAX)\n";
            }

            text += "\n";
            text += "Activity".TagColor(FontColor.Topic) + "\n";
            text += $"Meteors Analyzed: {meteors_found}\n";
            text += $"Touched Cleansed: {touched_cleansed}\n";
            text += "\n";
            text += "Current Benefits".TagColor(FontColor.Topic) + "\n";
            text += "- Basic meteor detection\n";
            if (rank >= GuildRank.Seeker)
            {
                text += "- Weave the Stars and Starforge crafting\n";
            }
            if (rank >= GuildRank.Researcher)
            {
                text += "- Closer meteor reports\n";
            }
            if (rank >= GuildRank.CosmosAddled)
            {
                text += "- Deeper cosmic disturbances\n";
            }
            if (rank >= GuildRank.CosmosApplied)
            {
                text += "- Ultima Projection boss scrolls\n";
            }
            if (rank >= GuildRank.Understander)
            {
                text += "- Astral Convergence Archivist scroll\n";
            }
            if (rank >= GuildRank.PrincipalStarseeker)
            {
                text += "- Doubled meteor core item rewards\n";
            }

            return text;
        }

        public override string GetTrackerText()
        {
            GuildRank rank = GetCurrentRank();
            GuildRank? nextRank = GetNextRank();
            if (nextRank != null)
            {
                return $"Skyreader: {FormatRankName(rank)} - {gp}/{(int)nextRank.Value} GP";
            }
            return $"Skyreader: {FormatRankName(rank)} - {gp} GP (MAX)";
        }


        [JsonProperty]
        public int gp;

        [JsonProperty]
        public int meteors_found;

        [JsonProperty]
        public int touched_cleansed;

    }

    

}
