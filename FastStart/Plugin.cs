using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace FastStartMod
{
    [BepInPlugin("mrmeagle.elin.faststart", "Fast Start", "1.1.0")]
    public class FastStartPlugin : BaseUnityPlugin
    {
        /// <summary>
        /// The prologue index we use for Fast Start mode.
        /// This is beyond the vanilla prologues list (0-3), so we intercept
        /// the Prologue getter to return the standard story prologue instead.
        /// </summary>
        public const int FastStartPrologueIndex = 100;

        /// <summary>
        /// Set to true while we are applying fast start rewards,
        /// so we can suppress noisy side effects if needed.
        /// </summary>
        internal static bool IsApplyingFastStart = false;

        private static FastStartPlugin _instance;

        /// <summary>
        /// Config entry for extra items to grant on fast start.
        /// Format: id:count,id:count,...
        /// </summary>
        internal static ConfigEntry<string> ExtraItemsConfig;

        void Awake()
        {
            _instance = this;

            ExtraItemsConfig = Config.Bind("ExtraItems", "Items",
                "money2:5,plat:20",
                "Extra items to grant on fast start. Format: id:count,id:count,...  Example: money2:5,plat:20,torch:1");

            var harmony = new Harmony("mrmeagle.elin.faststart");
            harmony.PatchAll();
            Logger.LogInfo("Fast Start mod loaded.");
        }

        internal static void Log(string msg) => _instance?.Logger.LogInfo(msg);
        internal static void LogWarn(string msg) => _instance?.Logger.LogWarning(msg);
    }

    // =========================================================================
    // Patch 1: Intercept Game.Prologue getter so our custom index doesn't crash
    // =========================================================================
    [HarmonyPatch(typeof(Game), nameof(Game.Prologue), MethodType.Getter)]
    static class Patch_Game_Prologue
    {
        static bool Prefix(Game __instance, ref Prologue __result)
        {
            if (__instance.idPrologue == FastStartPlugin.FastStartPrologueIndex)
            {
                // Return the standard story prologue (index 0) for zone/spawn settings
                __result = EClass.setting.start.prologues[0];
                return false;
            }
            return true;
        }
    }

    // =========================================================================
    // Patch 2: Add "Fast Start" to the mode selection list in UICharaMaker
    // =========================================================================
    [HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.SetChara))]
    static class Patch_UICharaMaker_SetChara
    {
        static void Postfix(UICharaMaker __instance)
        {
            // Append our custom mode to the list
            var modes = new List<string>(__instance.listMode);
            modes.Add("Fast Start");
            __instance.listMode = modes.ToArray();

            // If we're already set to our mode, update the display text
            if (EMono.game.idPrologue == FastStartPlugin.FastStartPrologueIndex)
            {
                __instance.textMode.SetText("Fast Start");
            }
        }
    }

    // =========================================================================
    // Patch 3: Handle selection of our mode in the ListModes popup
    // =========================================================================
    [HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.ListModes))]
    static class Patch_UICharaMaker_ListModes
    {
        static bool Prefix(UICharaMaker __instance)
        {
            // Replace the entire method with our version that handles
            // the extra "Fast Start" entry properly
            EMono.ui.AddLayer<LayerList>().SetStringList(
                () => __instance.listMode,
                delegate (int a, string b)
                {
                    if (a < EClass.setting.start.prologues.Count)
                    {
                        // Vanilla mode selected
                        EMono.game.idPrologue = a;
                        Prologue prologue = EMono.game.Prologue;
                        EMono.world.date.year = prologue.year;
                        EMono.world.date.month = prologue.month;
                        EMono.world.date.day = prologue.day;
                        EMono.world.weather._currentCondition = prologue.weather;
                    }
                    else
                    {
                        // Our custom "Fast Start" mode
                        EMono.game.idPrologue = FastStartPlugin.FastStartPrologueIndex;
                        // Use story prologue's date/weather settings
                        Prologue prologue = EClass.setting.start.prologues[0];
                        EMono.world.date.year = prologue.year;
                        EMono.world.date.month = prologue.month;
                        EMono.world.date.day = prologue.day;
                        EMono.world.weather._currentCondition = prologue.weather;
                    }
                    __instance.textMode.SetText(__instance.listMode[a]);
                    __instance.Refresh();
                }
            ).SetSize().SetTitles("wStartMode");

            return false; // skip original
        }
    }

    // =========================================================================
    // Allow Vernis to be claimed after quest completion
    // The game only allows claiming at phase 4, but our mod completes
    // the quest entirely (phase 999). 
    // =========================================================================
    [HarmonyPatch(typeof(Zone_Vernis), nameof(Zone_Vernis.isClaimable), MethodType.Getter)]
    static class Patch_Zone_Vernis_isClaimable
    {
        static void Postfix(Zone_Vernis __instance, ref bool __result)
        {
            if (!__result
                && EClass.game.quests.GetPhase<QuestVernis>() == 999
                && __instance.mainFaction != EClass.pc.faction)
            {
                __result = true;
            }
        }
    }

    // =========================================================================
    // Patch 5: Apply all Fast Start rewards after the game starts
    // =========================================================================
    [HarmonyPatch(typeof(Game), nameof(Game.StartNewGame))]
    static class Patch_Game_StartNewGame
    {
        static void Postfix()
        {
            if (EClass.game.idPrologue != FastStartPlugin.FastStartPrologueIndex)
                return;

            FastStartPlugin.Log("Applying Fast Start...");
            FastStartPlugin.IsApplyingFastStart = true;

            try
            {
                FastStartRewards.Apply();
            }
            finally
            {
                FastStartPlugin.IsApplyingFastStart = false;
            }

            FastStartPlugin.Log("Fast Start complete.");
        }
    }

    // =========================================================================
    // The actual reward/state injection logic
    // =========================================================================
    static class FastStartRewards
    {
        /// <summary>
        /// All quest IDs to mark as completed.
        /// These are the quests from game start through Vernis completion.
        /// </summary>
        static readonly string[] CompletedQuestIds = {
            // Tutorial
            "crafter",
            "sharedContainer",
            "tax",
            "introInspector",
            "shippingChest",
            "defense",
            // Post-tutorial home quests
            "loytel_farm",
            "puppy",
            // Exploration / Nymelle
            "exploration",
            "fiama_reward",
            "fiama_lock",
            // Fiama starter gift (day-7 dialog)
            "fiama_starter_gift",
            // Vernis prep chain
            "greatDebt",
            "farris_tulip",
            "kettle_join",
            "quru_morning",
            "quru_sing",
            "quru_past1",
            "quru_past2",
            // Vernis main quest
            "vernis_gold",
            // Post-Vernis
            "after_vernis",
        };

        /// <summary>
        /// Quest type names to mark as completed (prevents re-offering).
        /// </summary>
        static readonly string[] CompletedQuestTypes = {
            "QuestCrafter",
            "QuestSharedContainer",
            "QuestTax",
            "QuestIntroInspector",
            "QuestShippingChest",
            "QuestDefense",
            "QuestLoytelFarm",
            "QuestPuppy",
            "QuestExploration",
            "QuestFiamaLock",
            "QuestVernis",
            "QuestDialog",
        };

        /// <summary>
        /// NPC IDs to add as colony residents.
        /// </summary>
        static readonly string[] ResidentNpcIds = {
            "loytel",
            "farris",
            "kettle",
            "quru",
            "corgon",
            "demitas",
        };

        public static void Apply()
        {
            MarkQuestsComplete();
            SetMainQuestPhase();
            ClaimHomeZone();
            GrantRewardItems();
            GrantRecipes();
            AddResidentNpcs();
            MoveStoryNpcs();
            QueueFollowOnQuests();
            SetPlayerFlags();
            RemoveFaintCondition();
            GrantExtraItems();
        }

        /// <summary>
        /// Mark all tutorial/Vernis quests as completed in the quest manager.
        /// </summary>
        static void MarkQuestsComplete()
        {
            var quests = EClass.game.quests;

            foreach (var id in CompletedQuestIds)
            {
                if (!quests.completedIDs.Contains(id))
                    quests.completedIDs.Add(id);
            }

            foreach (var type in CompletedQuestTypes)
            {
                if (!quests.completedTypes.Contains(type))
                    quests.completedTypes.Add(type);
            }

            // Also complete the "home" quest and advance it
            if (!quests.completedIDs.Contains("home"))
                quests.completedIDs.Add("home");

            FastStartPlugin.Log($"Marked {CompletedQuestIds.Length} quests as completed.");
        }

        /// <summary>
        /// Set the main story quest to phase 700 (AfterAshLeaveHome).
        /// This is past all tutorial and Nymelle content.
        /// </summary>
        static void SetMainQuestPhase()
        {
            var mainQuest = EClass.game.quests.Get<QuestMain>();
            mainQuest.ChangePhase(700); // AfterAshLeaveHome
            FastStartPlugin.Log("Main quest set to phase 700 (AfterAshLeaveHome).");
        }

        /// <summary>
        /// Ensure the home zone is claimed as a PC faction zone.
        /// This is needed for NPCs to be added as branch members.
        /// </summary>
        static void ClaimHomeZone()
        {
            if (!EClass._zone.IsPCFaction)
            {
                EClass._zone.ClaimZone();
                FastStartPlugin.Log("Claimed home zone.");
            }
        }

        /// <summary>
        /// Grant all items that would have been received from the completed quests.
        /// </summary>
        static void GrantRewardItems()
        {
            var player = EClass.player;

            // --- From QuestCrafter.OnDropReward ---
            player.DropReward(ThingGen.Create("housePlate"));
            player.DropReward(ThingGen.Create("343"));
            player.DropReward(ThingGen.Create("432"));

            // --- From QuestTax.OnDropReward ---
            player.DropReward(ThingGen.Create("mailpost"));

            // --- From QuestDefense.OnDropReward ---
            player.DropReward(ThingGen.Create("plat").SetNum(10));

            // --- From DramaOutcome.QuestDefense_1 (stones, potions, bandages) ---
            player.DropReward(ThingGen.Create("stone").SetNum(20));
            player.DropReward(ThingGen.Create("330").SetNum(3));   // minor healing potions
            player.DropReward(ThingGen.Create("331").SetNum(3));   // minor mana potions
            player.DropReward(ThingGen.Create("bandage").SetNum(5));

            // --- From DramaOutcome.QuestSharedContainer_Drop1 ---
            player.DropReward(ThingGen.Create("chest6"));

            // --- From DramaOutcome.QuestShippingChest_Drop1 (shipping chest ingredients) ---
            Recipe.DropIngredients("container_shipping", "palm", 6);

            // --- From DramaOutcome.QuestCraft_Drop1 (straw material) ---
            player.DropReward(ThingGen.CreateRawMaterial(EClass.sources.materials.alias["straw"]));

            // --- From QuestPuppy.OnDropReward ---
            player.DropReward(ThingGen.Create("coolerbox"));

            // --- From QuestLoytelFarm.OnDropReward ---
            player.DropReward(TraitSeed.MakeSeed("pasture").SetNum(5));
            player.DropReward(TraitSeed.MakeSeed("tomato").SetNum(5));
            player.DropReward(TraitSeed.MakeSeed("kinoko").SetNum(5));

            // --- From DramaOutcome.QuestExploration_Drop1 (scrolls) ---
            player.DropReward(ThingGen.CreateScroll(8220)); // Return
            player.DropReward(ThingGen.CreateScroll(8221)); // Escape

            // --- From QuestVernis.OnChangePhase(7) ---
            player.DropReward(ThingGen.CreatePotion(8506).SetNum(3));
            player.DropReward(ThingGen.Create("blanket_fire"));

            // --- From QuestFiamaLock.OnStart (lockpick) ---
            var lockpick = ThingGen.Create("lockpick");
            lockpick.c_charges = 12;
            player.DropReward(lockpick);

            // --- From DramaOutcome.fiama_gold (10 gold bars) ---
            player.DropReward(ThingGen.Create("money2").SetNum(10));

            // --- From DramaOutcome.fiama_starter_gift (ring choice, index 0) ---
            var ring = ThingGen.Create("ring_decorative").SetNoSell();
            ring.elements.SetBase(65, 10);
            player.DropReward(ring);

            // --- Land deed for Vernis (lets player claim it at their leisure) ---
            player.DropReward(ThingGen.Create("deed"));

            FastStartPlugin.Log("Granted all reward items.");
        }

        /// <summary>
        /// Grant all recipes that would have been received from quest rewards.
        /// </summary>
        static void GrantRecipes()
        {
            var recipes = EClass.player.recipes;

            // From QuestCrafter
            GrantRecipe(recipes, "torch_wall");
            GrantRecipe(recipes, "factory_sign");

            // From QuestDialog fiama_reward
            GrantRecipe(recipes, "workbench2");
            GrantRecipe(recipes, "factory_stone");
            GrantRecipe(recipes, "stonecutter");

            // From DramaOutcome.QuestVernis_DropRecipe
            GrantRecipe(recipes, "explosive");

            FastStartPlugin.Log("Granted all recipes.");
        }

        static void GrantRecipe(RecipeManager recipes, string id)
        {
            if (!recipes.knownRecipes.ContainsKey(id))
                recipes.knownRecipes[id] = 1;
        }

        /// <summary>
        /// Add key NPCs as residents of the player's home colony.
        /// Follows the same pattern as CoreDebug.Story_Test.
        /// </summary>
        static void AddResidentNpcs()
        {
            foreach (var npcId in ResidentNpcIds)
            {
                AddResident(npcId);
            }
            FastStartPlugin.Log($"Added {ResidentNpcIds.Length} resident NPCs.");
        }

        /// <summary>
        /// Add a single NPC as a resident, following CoreDebug's pattern.
        /// </summary>
        static void AddResident(string id)
        {
            // Check if already a global chara
            var chara = EClass.game.cards.globalCharas.Find(id);
            if (chara == null)
            {
                // Create and register as global
                chara = CharaGen.Create(id);
                chara.SetGlobal();
            }

            // Add to the zone and branch
            var spawnPoint = EClass.pc.pos.GetNearestPoint(
                allowBlock: false, allowChara: false);

            if (chara.currentZone != EClass._zone)
            {
                EClass._zone.AddCard(chara, spawnPoint);
            }

            if (!EClass.Branch.members.Contains(chara))
            {
                EClass.Branch.AddMemeber(chara);
            }

            // Corgon gets special flag (from QuestVernis.OnComplete)
            if (id == "corgon")
            {
                chara.SetInt(100, 1);
            }

            FastStartPlugin.Log($"  Added resident: {id}");
        }

        /// <summary>
        /// Move Ash and Fiama to Lothria (they leave home after exploration).
        /// This matches DramaOutcome.QuestExploration_AfterComplete.
        /// </summary>
        static void MoveStoryNpcs()
        {
            var ash = EClass.game.cards.globalCharas.Find("ashland");
            ash.MoveHome("lothria", 40, 49);
            EClass.game.quests.RemoveAll(ash);
            FastStartPlugin.Log("Moved Ashland to Lothria.");

            var fiama = EClass.game.cards.globalCharas.Find("fiama");
            fiama.MoveHome("lothria", 46, 56);
            EClass.game.quests.RemoveAll(fiama);
            FastStartPlugin.Log("Moved Fiama to Lothria.");
        }

        /// <summary>
        /// Queue the follow-on quests that would normally start after Vernis.
        /// </summary>
        static void QueueFollowOnQuests()
        {
            var quests = EClass.game.quests;

            // From QuestVernis.OnComplete: mokyu + pre_debt
            quests.Add("mokyu", "corgon").startDate =
                EClass.world.date.GetRaw() + 14400;

            quests.Add("pre_debt", "farris").startDate =
                EClass.world.date.GetRaw() + 28800;

            // From QuestDialog exile_quru: into_darkness
            quests.Add("into_darkness", "kettle").startDate =
                EClass.world.date.GetRaw() + 7200;

            FastStartPlugin.Log("Queued follow-on quests (mokyu, pre_debt, into_darkness).");
        }

        /// <summary>
        /// Set player flags to skip tutorial UI hints.
        /// </summary>
        static void SetPlayerFlags()
        {
            EClass.player.flags.welcome = true;
            EClass.player.flags.toggleHotbarHighlightActivated = true;
            FastStartPlugin.Log("Set player flags.");
        }

        /// <summary>
        /// Remove the ConFaint condition that the story prologue applies.
        /// </summary>
        static void RemoveFaintCondition()
        {
            EClass.pc.RemoveCondition<ConFaint>();
            FastStartPlugin.Log("Removed faint condition.");
        }

        /// <summary>
        /// Grant extra items from the BepInEx config file.
        /// Format: id:count,id:count,...
        /// </summary>
        static void GrantExtraItems()
        {
            var configValue = FastStartPlugin.ExtraItemsConfig.Value;
            if (string.IsNullOrWhiteSpace(configValue)) return;

            foreach (var entry in configValue.Split(','))
            {
                var trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var parts = trimmed.Split(':');
                string id = parts[0].Trim();
                int count = parts.Length > 1 ? int.Parse(parts[1].Trim()) : 1;

                EClass.player.DropReward(ThingGen.Create(id).SetNum(count));
                FastStartPlugin.Log($"  Extra item: {id} x{count}");
            }

            FastStartPlugin.Log("Granted extra items from config.");
        }
    }
}
