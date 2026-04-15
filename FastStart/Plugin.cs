using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace FastStartMod
{
    [BepInPlugin("mrmeagle.elin.faststart", "Fast Start", "1.1.0")]
    public class FastStartPlugin : BaseUnityPlugin
    {
        public const string FastStartLabel = "Fast Start";
        internal static int RegisteredPrologueIndex = -1;

        internal static bool IsApplyingFastStart = false;
        internal static bool HasAppliedFastStart = false;

        private static FastStartPlugin _instance;

        internal static ConfigEntry<string> ExtraItemsConfig;

        void Awake()
        {
            _instance = this;

            ExtraItemsConfig = Config.Bind(
                "ExtraItems",
                "Items",
                "money2:12,plat:20",
                "Extra items to grant on fast start. Format: id:count,id:count,...  Example: money2:5,plat:20,torch:1");

            var harmony = new Harmony("mrmeagle.elin.faststart");
            harmony.PatchAll();
            Logger.LogInfo("Fast Start mod loaded.");
        }

        internal static void Log(string msg) => _instance?.Logger.LogInfo(msg);
        internal static void LogWarn(string msg) => _instance?.Logger.LogWarning(msg);
        internal static void LogError(string msg) => _instance?.Logger.LogError(msg);

        internal static int EnsureFastStartPrologueRegistered()
        {
            if (RegisteredPrologueIndex >= 0
                && EClass.setting?.start?.prologues != null
                && RegisteredPrologueIndex < EClass.setting.start.prologues.Count)
            {
                return RegisteredPrologueIndex;
            }

            if (EClass.setting?.start?.prologues == null || EClass.setting.start.prologues.Count == 0)
            {
                LogWarn("Unable to register Fast Start because the base prologue list is unavailable.");
                return -1;
            }

            Prologue template = EClass.setting.start.prologues[0];
            EClass.setting.start.prologues.Add(new Prologue
            {
                type = template.type,
                idStartZone = template.idStartZone,
                startX = template.startX,
                startZ = template.startZ,
                year = template.year,
                month = template.month,
                day = template.day,
                hour = template.hour,
                posAsh = template.posAsh,
                posFiama = template.posFiama,
                posPunk = template.posPunk,
                weather = template.weather,
            });
            RegisteredPrologueIndex = EClass.setting.start.prologues.Count - 1;
            Log($"Registered Fast Start prologue at index {RegisteredPrologueIndex}.");
            return RegisteredPrologueIndex;
        }
    }

    [HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.SetChara))]
    static class Patch_UICharaMaker_SetChara
    {
        static void Postfix(UICharaMaker __instance)
        {
            int fastStartIndex = FastStartPlugin.EnsureFastStartPrologueRegistered();
            if (fastStartIndex < 0)
            {
                return;
            }

            var modes = new List<string>(__instance.listMode);
            if (!modes.Contains(FastStartPlugin.FastStartLabel))
            {
                modes.Add(FastStartPlugin.FastStartLabel);
                __instance.listMode = modes.ToArray();
            }

            if (EMono.game.idPrologue == fastStartIndex)
            {
                __instance.textMode.SetText(FastStartPlugin.FastStartLabel);
            }
        }
    }

    [HarmonyPatch(typeof(Zone_Vernis), nameof(Zone_Vernis.isClaimable), MethodType.Getter)]
    static class Patch_Zone_Vernis_isClaimable
    {
        static void Postfix(Zone_Vernis __instance, ref bool __result)
        {
            if (!__result
                && EClass.game.quests.GetPhase<QuestVernis>() == Quest.PhaseComplete
                && __instance.mainFaction != EClass.pc.faction)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.ShowCompleteText))]
    static class Patch_Quest_ShowCompleteText
    {
        static bool Prefix() => !FastStartPlugin.IsApplyingFastStart;
    }

    [HarmonyPatch(typeof(QuestDialog), nameof(QuestDialog.ShowCompleteText))]
    static class Patch_QuestDialog_ShowCompleteText
    {
        static bool Prefix() => !FastStartPlugin.IsApplyingFastStart;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.UpdateJournal))]
    static class Patch_Quest_UpdateJournal
    {
        static bool Prefix() => !FastStartPlugin.IsApplyingFastStart;
    }

    [HarmonyPatch(typeof(Game), nameof(Game.StartNewGame))]
    static class Patch_Game_StartNewGame
    {
        static void Prefix()
        {
            FastStartPlugin.HasAppliedFastStart = false;
        }

        static void Postfix()
        {
            if (EClass.game.idPrologue != FastStartPlugin.RegisteredPrologueIndex)
            {
                return;
            }

            if (FastStartPlugin.HasAppliedFastStart)
            {
                FastStartPlugin.LogWarn("Fast Start bootstrap already ran for this new game.");
                return;
            }

            FastStartPlugin.HasAppliedFastStart = true;
            FastStartPlugin.Log("Applying Fast Start...");
            FastStartPlugin.IsApplyingFastStart = true;
            bool succeeded = false;

            try
            {
                FastStartBootstrap.Apply();
                succeeded = true;
            }
            catch (Exception ex)
            {
                FastStartPlugin.LogError($"Fast Start failed: {ex}");
            }
            finally
            {
                FastStartPlugin.IsApplyingFastStart = false;
            }

            if (succeeded)
            {
                FastStartPlugin.Log("Fast Start complete.");
            }
        }
    }

    static class FastStartBootstrap
    {
        const string FiamaGiftCompanionId = "shojo";

        public static void Apply()
        {
            RequireStartZone();
            EnsureCoreQuests();
            ClaimStartingHome();
            ReplayTutorialHomeChain();
            ReplayExplorationChain();
            ReplayPostExplorationDialogChain();
            ReplayVernisQuest();
            GrantFiamaGiftReplacement();
            GrantBaselineTools();
            SetPlayerFlags();
            RemoveFaintCondition();
            GrantExtraItems();
        }

        static void RequireStartZone()
        {
            if (EClass._zone != EClass.game.StartZone)
            {
                throw new InvalidOperationException("Fast Start bootstrap must run in the start zone.");
            }
        }

        static void EnsureCoreQuests()
        {
            if (EClass.game.quests.Get<QuestMain>() == null)
            {
                EClass.game.quests.Start("main");
            }

            if (EClass.game.quests.Get<QuestHome>() == null)
            {
                EClass.game.quests.Start("home");
            }
        }

        static void ClaimStartingHome()
        {
            ConsumeOnePlayerItem("deed");

            if (!EClass._zone.IsPCFaction)
            {
                EClass._zone.ClaimZone();
                FastStartPlugin.Log("Claimed the starting home zone.");
            }

            QuestHome home = EClass.game.quests.Get<QuestHome>()
                ?? throw new InvalidOperationException("QuestHome is missing during Fast Start bootstrap.");
            QuestMain main = EClass.game.quests.Get<QuestMain>()
                ?? throw new InvalidOperationException("QuestMain is missing during Fast Start bootstrap.");

            if (home.phase < 1)
            {
                home.ChangePhase(1);
            }

            if (main.phase < 200)
            {
                main.ChangePhase(200);
            }

            AddGlobalQuestIfMissing("sharedContainer", "ashland");
            AddGlobalQuestIfMissing("crafter", "ashland");
            AddGlobalQuestIfMissing("defense", "ashland");

            if (home.phase < 2)
            {
                home.ChangePhase(2);
            }
        }

        static void ReplayTutorialHomeChain()
        {
            FastStartPlugin.Log("Replaying tutorial and home quest chain.");

            GrantSharedContainerReward();
            CompleteGlobalQuest("sharedContainer");

            GrantCrafterSetupReward();
            CompleteGlobalQuest("crafter");

            GrantDefenseCombatRewards();
            Quest defense = StartQuest("defense");
            defense.ChangePhase(2);
            defense.Complete();

            CompleteGlobalQuest("puppy");
            CompleteGlobalQuest("tax");
            CompleteGlobalQuest("introInspector");

            GrantShippingChestIngredients();
            CompleteGlobalQuest("shippingChest");
            CompleteGlobalQuest("loytel_farm");
        }

        static void ReplayExplorationChain()
        {
            FastStartPlugin.Log("Replaying exploration and Fiama chain.");

            QuestExploration exploration = StartQuest("exploration") as QuestExploration
                ?? throw new InvalidOperationException("Quest 'exploration' did not resolve to QuestExploration.");

            FastStartPlugin.Log("  Completing fiama_reward.");
            CompleteGlobalQuest("fiama_reward");
            FastStartPlugin.Log("  Completing fiama_lock.");
            CompleteGlobalQuest("fiama_lock");

            FastStartPlugin.Log("  Granting exploration travel scrolls.");
            GrantExplorationTravelScrolls();
            FastStartPlugin.Log("  Recruiting Farris and advancing exploration.");
            ApplyMeetFarris(exploration);
            exploration.ChangePhase(5);
            FastStartPlugin.Log("  Completing exploration and moving Ash/Fiama out.");
            exploration.Complete();
            ApplyAfterExplorationComplete();
        }

        static void ReplayPostExplorationDialogChain()
        {
            FastStartPlugin.Log("Replaying post-exploration dialog chain.");

            CompleteGlobalQuest("greatDebt");
            CompleteGlobalQuest("farris_tulip");
            CompleteGlobalQuest("kettle_join");
            CompleteGlobalQuest("quru_morning");
            CompleteGlobalQuest("quru_sing");
            CompleteGlobalQuest("quru_past1");
            CompleteGlobalQuest("quru_past2");
        }

        static void ReplayVernisQuest()
        {
            FastStartPlugin.Log("Replaying Vernis quest.");

            QuestVernis vernis = StartQuest("vernis_gold") as QuestVernis
                ?? throw new InvalidOperationException("Quest 'vernis_gold' did not resolve to QuestVernis.");

            EClass.player.DropReward(ThingGen.CreateRecipe("explosive"));
            EClass.player.DropReward(ThingGen.Create("deed"));

            vernis.ChangePhase(1);
            vernis.ChangePhase(5);
            vernis.ChangePhase(7);
            vernis.Complete();
            NormalizePostVernisResidents();
        }

        static void ApplyMeetFarris(QuestExploration exploration)
        {
            Chara farris = FindOrCreateGlobalChara("farris");

            exploration.ChangePhase(1);
            farris.RemoveEditorTag(EditorTag.AINoMove);
            farris.RemoveEditorTag(EditorTag.InvulnerableToMobs);
            farris.RemoveEditorTag(EditorTag.Invulnerable);
            farris.homeZone = EClass.game.StartZone;
            farris.MoveZone(EClass.game.StartZone, ZoneTransition.EnterState.Return);

            exploration.ChangePhase(2);
            EClass.Branch.Recruit(farris);
            EnsureMainQuestPhase(300);
        }

        static void ApplyAfterExplorationComplete()
        {
            Chara ash = RequireGlobalChara("ashland");
            Chara fiama = RequireGlobalChara("fiama");

            ash.MoveHome("lothria", 40, 49);
            EClass.game.quests.RemoveAll(ash);

            fiama.MoveHome("lothria", 46, 56);
            EClass.game.quests.RemoveAll(fiama);

            SuppressTutorialHooks();
            EnsureMainQuestPhase(700);
        }

        static void EnsureMainQuestPhase(int phase)
        {
            QuestMain main = EClass.game.quests.Get<QuestMain>();
            if (main == null)
            {
                main = EClass.game.quests.Start("main") as QuestMain
                    ?? throw new InvalidOperationException("Failed to start QuestMain.");
            }

            if (main.phase < phase)
            {
                main.ChangePhase(phase);
            }
        }

        static Quest CompleteGlobalQuest(string id)
        {
            Quest quest = StartQuest(id);
            quest.Complete();
            return quest;
        }

        static Quest StartQuest(string id)
        {
            Quest started = EClass.game.quests.Get(id);
            if (started != null)
            {
                return started;
            }

            Quest global = EClass.game.quests.GetGlobal(id);
            if (global != null)
            {
                EClass.game.quests.RemoveGlobal(global);
                return EClass.game.quests.Start(global);
            }

            return EClass.game.quests.Start(id);
        }

        static void AddGlobalQuestIfMissing(string id, string clientId = null)
        {
            if (EClass.game.quests.IsCompleted(id)
                || EClass.game.quests.Get(id) != null
                || EClass.game.quests.GetGlobal(id) != null)
            {
                return;
            }

            Quest quest = string.IsNullOrEmpty(clientId)
                ? Quest.Create(id)
                : Quest.Create(id).SetClient(RequireGlobalChara(clientId), assignQuest: false);

            EClass.game.quests.globalList.Add(quest);
        }

        static Chara RequireGlobalChara(string id)
        {
            Chara chara = EClass.game.cards.globalCharas.Find(id);
            if (chara == null)
            {
                throw new InvalidOperationException($"Required global character '{id}' was not found.");
            }

            return chara;
        }

        static Chara FindOrCreateGlobalChara(string id)
        {
            Chara chara = EClass.game.cards.globalCharas.Find(id);
            if (chara != null)
            {
                return chara;
            }

            FastStartPlugin.Log($"Creating missing global character '{id}' for Fast Start.");
            chara = CharaGen.Create(id);
            chara.SetGlobal();
            return chara;
        }

        static void SuppressTutorialHooks()
        {
            RemoveQuestIfPresent("ash1");
            RemoveQuestIfPresent("ash2");
            RemoveQuestIfPresent("fiama1");
            RemoveQuestIfPresent("fiama2");

            EClass.player.dialogFlags["ash1"] = 1;
            EClass.player.dialogFlags["fiama1"] = 1;
        }

        static void NormalizePostVernisResidents()
        {
            FastStartPlugin.Log("Normalizing post-Vernis residents to the starting zone.");

            string[] residentIds = { "loytel", "farris", "kettle", "quru", "corgon" };
            Point[] offsets =
            {
                new Point(-2, -1),
                new Point(-1, 1),
                new Point(1, -1),
                new Point(2, 1),
                new Point(0, 2),
            };

            for (int i = 0; i < residentIds.Length; i++)
            {
                MoveResidentToStartZone(residentIds[i], offsets[i].x, offsets[i].z);
            }
        }

        static void MoveResidentToStartZone(string id, int offsetX, int offsetZ)
        {
            Chara resident = RequireGlobalChara(id);
            EnsureBranchMembership(resident);

            Point target = new Point(EClass.pc.pos.x + offsetX, EClass.pc.pos.z + offsetZ)
                .GetNearestPoint(allowBlock: false, allowChara: false);
            resident.MoveHome(EClass.game.StartZone, target.x, target.z);
        }

        static void EnsureBranchMembership(Chara resident)
        {
            if (resident.homeBranch != EClass.Branch || !EClass.Branch.members.Contains(resident))
            {
                EClass.Branch.AddMemeber(resident);
            }
        }

        static void GrantFiamaGiftReplacement()
        {
            FastStartPlugin.Log("Granting Fiama Fast Start replacement gift: shojo.");

            Point target = new Point(EClass.pc.pos.x + 1, EClass.pc.pos.z + 2)
                .GetNearestPoint(allowBlock: false, allowChara: false);
            Chara companion = EClass._zone.AddCard(CharaGen.Create(FiamaGiftCompanionId), target).Chara;
            companion.MakeAlly();
            companion.SetInt(100, 1);
        }

        static void RemoveQuestIfPresent(string id)
        {
            Quest started = EClass.game.quests.Get(id);
            if (started != null)
            {
                EClass.game.quests.Remove(started);
            }

            Quest global = EClass.game.quests.GetGlobal(id);
            if (global != null)
            {
                EClass.game.quests.RemoveGlobal(global);
            }
        }

        static void ConsumeOnePlayerItem(string id)
        {
            Thing thing = EClass.pc.things.Find(id);
            if (thing == null)
            {
                return;
            }

            thing.ModNum(-1);
        }

        static void GrantSharedContainerReward()
        {
            EClass.player.DropReward(ThingGen.Create("chest6"));
        }

        static void GrantCrafterSetupReward()
        {
            EClass.player.DropReward(ThingGen.CreateRawMaterial(EClass.sources.materials.alias["straw"]));
        }

        static void GrantShippingChestIngredients()
        {
            Recipe.DropIngredients("container_shipping", "palm", 6);
        }

        static void GrantDefenseCombatRewards()
        {
            EClass.player.DropReward(ThingGen.Create("stone").SetNum(20));
            EClass.player.DropReward(ThingGen.Create("330").SetNum(3), silent: true).Identify(show: false);
            EClass.player.DropReward(ThingGen.Create("331").SetNum(3), silent: true).Identify(show: false);
            EClass.player.DropReward(ThingGen.Create("bandage").SetNum(5));
        }

        static void GrantExplorationTravelScrolls()
        {
            EClass.player.DropReward(ThingGen.CreateScroll(8220), silent: true).c_IDTState = 0;
            EClass.player.DropReward(ThingGen.CreateScroll(8221)).c_IDTState = 0;
        }

        static void GrantBaselineTools()
        {
            FastStartPlugin.Log("Granting baseline tools.");

            GrantTool("axe", "iron");
            GrantTool("hoe", "granite");
            GrantTool("shovel", "granite");
            GrantTool("pickaxe", "granite");
            GrantTool("hammer", "granite");
        }

        static void GrantTool(string id, string material)
        {
            Thing tool = ThingGen.Create(id);
            tool.ChangeMaterial(material);
            EClass.player.DropReward(tool);
        }

        static void SetPlayerFlags()
        {
            EClass.player.flags.welcome = true;
            EClass.player.flags.toggleHotbarHighlightActivated = true;
        }

        static void RemoveFaintCondition()
        {
            EClass.pc.RemoveCondition<ConFaint>();
        }

        static void GrantExtraItems()
        {
            string configValue = FastStartPlugin.ExtraItemsConfig.Value;
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return;
            }

            foreach (string entry in configValue.Split(','))
            {
                string trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                string[] parts = trimmed.Split(':');
                string id = parts[0].Trim();
                int count = 1;
                if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out count))
                {
                    FastStartPlugin.LogWarn($"Invalid extra item count in config entry '{trimmed}'.");
                    continue;
                }

                EClass.player.DropReward(ThingGen.Create(id).SetNum(count));
                FastStartPlugin.Log($"  Extra item: {id} x{count}");
            }
        }
    }
}
