using System;
using System.IO;
using System.Runtime.Remoting.Messaging;
using BepInEx;
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
        internal const string Version = "1.0";
    }

    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    internal class SkyreaderGuild : BaseUnityPlugin
    {

        // Import assets
        public void OnStartCore()
        {
            var s = Core.Instance.sources;
            var f = Path.GetDirectoryName(Info.Location) + "/Assets/SourceCard.xlsx";
            ModUtil.ImportExcel(f, "Chara", s.charas);
            ModUtil.ImportExcel(f, "CharaText", s.charaText);
            ModUtil.ImportExcel(f, "Thing", s.things);
            this.AddQuest(s);

            //string dump = JsonConvert.SerializeObject(s.quests);
            //SkyreaderGuild.Log(dump);


            /*if(!EClass.game.quests.IsStarted<QuestSkyreader>() && !EClass.game.quests.IsCompleted("skyreader_guild"))
            {
                //EClass.game.quests.Add("skyreader_guild", "srg_arkyn").startDate = EClass.world.date.GetRaw(0)+1440;
            }*/
        }

        private void AddQuest(SourceManager sources)
        {
            //Sets up the quest's information so it can be instantiated later
            var quest = sources.quests.CreateRow();
            //ClassCache.assemblies.Add("Mod_KiriaDLC");
            quest.id = "skyreader_guild";
            quest.name = "the skyreader's guild";
            quest.name_JP = "..."; //TODO
                                   //If you have a quest lcass, specify it here, you can use a mod namespace, see
                                   //QuestKiria.cs
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

        private void Awake()
        {

            Harmony harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();

        }

        internal static void Log(string payload)
        {
            Console.WriteLine("[SkyreaderGuild]" + payload);
        }
    }

    // TraitBook patch for OnRead(Chara c)

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
                    EClass.world.FindZone("somewhere").AddCard(c);
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

                __instance.owner.ModNum(-1, true);
                return false;
            }
            return true;
            
        }  
        
    }
    /*
    // Don't execute regular TraitScrollMap setname
    [HarmonyPatch(typeof(TraitScrollMap), "SetName")]
    public static class YithSetNamePatch
    {
        public static bool Prefix(ref TraitScrollMap __instance)
        {
            SkyreaderGuild.Log("Checking " +  __instance.owner.id.ToString() + " to see if its srg_starchart...");
            if (__instance.owner.id == "srg_starchart") return false;
            return true;
        }

       private static void AddQuest(SourceManager sources)
    {
        //Sets up the quest's information so it can be instantiated later
        var quest = sources.quests.CreateRow();
        //ClassCache.assemblies.Add("Mod_KiriaDLC");
        quest.id = "kiria_map_quest";
        quest.name = "Investigation request";
        quest.name_JP = "..."; //TODO
        //If you have a quest lcass, specify it here, you can use a mod namespace, see
        //QuestKiria.cs
        quest.type = "Mod_KiriaDLC.QuestKiria";
        //The message on the quest board.
        //For multistep quests, each step's description is separated by pipes.
        quest.detail =
            "#pc, can you look into something for me?|You've recieved a map from Kiria, find out where it points.";
        quest.detail_JP =
            "#pc, ..."; //TODO
        quest.drama = ["kiriaDLC", "main"];
        sources.quests.rows.Add(quest);

    [HarmonyPatch(typeof(Zone))]
    [HarmonyPatch(nameof(Zone.Activate))]
    class ZonePatch : EClass {
    static void Prefix(Zone __instance)
    {
        Debug.LogWarning("Zone.Activate() called:");
        Debug.LogWarning("\t" + __instance.NameWithDangerLevel);
        Debug.LogWarning("\t" + __instance.pathExport);
    }
    static void Postfix(Zone __instance) {
        Debug.LogWarning("Now entering " + __instance.source.id);
        //If they've already gotten the quest, or the quest is finished, we don't want to add it again
        //Also make sure it's the PC's zone and that it's not already in the list to avoid duplication
        //and issues with moongates.
        //This is a one and done quest
        if (!EClass.game.quests.IsCompleted("kiria_map_quest") 
            && !EClass.game.quests.IsStarted<QuestKiria>()
            && EClass._zone.IsPCFaction
            && EClass.game.quests.globalList.All(x => x.id != "kiria_map_quest")
            )
        {
            //Quest must have a client, we find Kiria to be the client
            Chara c = EClass.game.cards.globalCharas.Find("adv_kiria");
            //If Kiria is recruited and has enough affinity, add the quest 
            if (c != null) // && c.IsPCFaction && c.affinity.value >= 85) //Pre marriage, post recruit
            {
                //Putting it on the global quest list and setting the client will make the quest
                //Appear on the quest board
                Debug.Log("KiriaDLC:: Adding quest to global list");
                EClass.game.quests.globalList.Add(Quest.Create("kiria_map_quest").SetClient(c, false));
            }
            
        }
    }
}
    }
    
    }*/

    // Drop the map when we kill this yith
    [HarmonyPatch(typeof(Card), "SpawnLoot")]
    public static class YithDropStarchart
    {
        public static void Postfix(ref Card __instance)
        {
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

    // Spawn our yith blob as appropriate
    [HarmonyPatch(typeof(Zone), "OnVisit")]
    public static class SpawnYithGrowthOnVisit
    {
        public static void Postfix(Zone __instance)
        {
            // Check if the current zone is a Nefia
            SkyreaderGuild.Log("Entering zone, rolling dice to see if we spawn yith");
            if (!__instance.IsNefia) return;
            if (__instance.DangerLv < 15) return;
            if (EClass.game.quests.IsCompleted("skyreader_guild") ||
                    EClass.game.quests.IsStarted<QuestSkyreader>()) return;
            if (EClass.rnd(100) > 20) return; // 20% chance to spawn one

            // Ensure the specific Chara does not already exist in the zone
            Chara existingChara = __instance.FindChara("srg_growth");
            if (existingChara != null) { SkyreaderGuild.Log("existing growth found!"); return; }

            // Spawn the specific Chara
            Point spawnPoint = __instance.GetRandomVisitPos(EClass.pc);
            var newChara = __instance.AddChara("srg_growth", spawnPoint.x, spawnPoint.z);
            SkyreaderGuild.Log($"Spawned specific Chara {newChara.id} in Nefia {__instance.id} at {spawnPoint.x} / {spawnPoint.z} - (Level {__instance.DangerLv}).");

        }
    }


    // EClass.game.quests.start("skyreader_guild", owner, false)
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
            if (phase >= (int)GuildRank.PrincipalStarseeker) return GuildRank.PrincipalStarseeker;
            if (phase >= (int)GuildRank.Understander) return GuildRank.Understander;
            if (phase >= (int)GuildRank.CosmosApplied) return GuildRank.CosmosApplied;
            if (phase >= (int)GuildRank.CosmosAddled) return GuildRank.CosmosAddled;
            if (phase >= (int)GuildRank.Researcher) return GuildRank.Researcher;
            if (phase >= (int)GuildRank.Seeker) return GuildRank.Seeker;
            return GuildRank.Wanderer;
        }


        [JsonProperty]
        public int gp;

        [JsonProperty]
        public int meteors_found;

        [JsonProperty]
        public int touched_cleansed;

    }

    


}