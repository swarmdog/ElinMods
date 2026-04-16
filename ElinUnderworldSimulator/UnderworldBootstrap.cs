using System;
using System.Collections.Generic;
using System.Linq;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldBootstrap
    {
        private static readonly HashSet<string> SuppressedQuestIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "main",
            "home",
            "sharedContainer",
            "crafter",
            "defense",
            "tax",
            "introInspector",
            "shippingChest",
            "exploration",
            "fiama_reward",
            "fiama_lock",
            "loytel_farm",
            "fiama_starter_gift",
            "puppy",
        };

        internal static void Apply()
        {
            RequireStartZone();
            UnderworldRuntime.ResetForNewGame();
            if (!EClass._zone.IsPCFaction)
            {
                EClass._zone.ClaimZone();
            }

            MaintainStoryIsolation();

            Thing mixingTable = EnsurePlacedFixture(ModInfo.MixingTableId, EClass.pc.pos, (2, 1), (2, 0), (1, 1));
            Thing contrabandChest = EnsurePlacedFixture(ModInfo.ContrabandChestId, EClass.pc.pos, (-2, 1), (-2, 0), (-1, 1));
            Thing starterStorage = EnsureStarterStorage(contrabandChest ?? mixingTable);
            EnsureStarterFurniture(starterStorage);

            Chara fixer = EnsureFixerInStartZone();
            EnsureFixerInPlayerBranch(fixer);
            GrantStarterItems(starterStorage);
            UnderworldPlugin.Log("Underworld startup applied.");
        }

        internal static void MaintainStoryIsolation()
        {
            MoveStoryNpcOffsite("ashland");
            MoveStoryNpcOffsite("fiama");
            RemoveSuppressedQuests();
        }

        private static void RequireStartZone()
        {
            if (EClass._zone != EClass.game.StartZone)
            {
                throw new InvalidOperationException("Underworld startup must run inside the start zone.");
            }
        }

        private static Thing EnsurePlacedFixture(string id, Point anchor, params (int x, int z)[] preferredOffsets)
        {
            Thing fixture = FindThingById(id);
            if (fixture != null)
            {
                if (!fixture.IsInstalled)
                {
                    fixture.Install();
                }

                return fixture;
            }

            Point desired = FindPlacementPoint(anchor, preferredOffsets);
            if (desired == null)
            {
                throw new InvalidOperationException($"Unable to place startup fixture '{id}'.");
            }

            fixture = EClass._zone.AddCard(ThingGen.Create(id), desired).Thing;
            fixture.Install();
            return fixture;
        }

        private static Thing EnsureStarterStorage(Thing anchor)
        {
            if (anchor?.pos == null)
            {
                throw new InvalidOperationException("Starter storage placement requires a valid anchor.");
            }

            return EnsurePlacedFixture("chest6", anchor.pos, (1, 0), (1, 1), (0, 1), (-1, 0));
        }

        private static void EnsureStarterFurniture(Thing anchor)
        {
            if (anchor?.pos == null)
            {
                throw new InvalidOperationException("Starter furniture placement requires a valid storage anchor.");
            }

            EnsurePlacedFixture("table", anchor.pos, (1, 0), (0, 1), (-1, 0));
            EnsurePlacedFixture("chair", anchor.pos, (0, 1), (1, 1), (-1, 1));
        }

        private static Chara EnsureFixerInStartZone()
        {
            Chara fixer = FindExistingFixer() ?? CharaGen.Create(ModInfo.FixerId);
            Point placement = ResolveFixerPlacementPoint();

            if (fixer.currentZone != EClass._zone || fixer.pos == null || !fixer.pos.IsValid)
            {
                fixer.MoveHome(EClass._zone, placement.x, placement.z);
            }
            else if (!fixer.IsGlobal)
            {
                fixer.SetHomeZone(EClass._zone);
            }

            return fixer;
        }

        private static void EnsureFixerInPlayerBranch(Chara fixer)
        {
            if (fixer == null)
            {
                throw new InvalidOperationException("Underworld startup requires a Fixer instance.");
            }

            if (EClass.Branch == null)
            {
                throw new InvalidOperationException("Underworld startup requires the claimed Meadow branch.");
            }

            EClass.Branch.AddMemeber(fixer);
        }

        private static Chara FindExistingFixer()
        {
            Chara fixer = EClass._map.charas.FirstOrDefault(chara => chara != null && !chara.isDestroyed && chara.id == ModInfo.FixerId);
            if (fixer != null)
            {
                return fixer;
            }

            return EClass.game?.cards?.globalCharas?.Find(ModInfo.FixerId);
        }

        private static Point ResolveFixerPlacementPoint()
        {
            Point point = EClass.pc.pos.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false, ignoreCenter: true);
            if (point != null)
            {
                return point;
            }

            throw new InvalidOperationException("Unable to find a valid placement point for the Fixer.");
        }

        private static Thing[] CreateDirectStarterRewards()
        {
            return new[]
            {
                CreateReward(ModInfo.DealerLedgerId, 1),
                CreateReward(ModInfo.SampleKitId, 1),
                CreateReward(ModInfo.AntidoteId, 2),
            };
        }

        private static Thing[] CreateStoredStarterRewards()
        {
            return new[]
            {
                // Vanilla early-base flow assumes these tools are available.
                // CoreDebug seeds hoe/shovel/axe/pickaxe/hammer, and Loytel farm explicitly checks hoe + shovel.
                CreateReward("hoe", 1),
                CreateReward("shovel", 1),
                CreateReward("axe", 1),
                CreateReward("pickaxe", 1),
                CreateReward("hammer", 1),
                CreateReward(UnderworldContentIds.HerbWhisperId, 8),
                CreateReward(UnderworldContentIds.HerbDreamId, 4),
                CreateReward(UnderworldContentIds.HerbShadowId, 6),
                CreateReward(UnderworldContentIds.MineralCrudeId, 5),
                CreateReward("potion_empty", 10),
                // Debug-only: guarantee one complete Whisper Tonic crafting path for iteration.
                // This bundle exists for debug/testing and is not meant as lore or progression content.
                CreateReward(UnderworldContentIds.HerbWhisperId, 6),
                CreateReward(UnderworldContentIds.MineralCrudeId, 3),
                CreateReward("potion_empty", 2),
                TraitSeed.MakeSeed(UnderworldContentIds.CropWhisperId).SetNum(3),
                TraitSeed.MakeSeed(UnderworldContentIds.CropShadowId).SetNum(2),
            };
        }

        private static Thing CreateReward(string id, int amount)
        {
            return ThingGen.Create(id).SetNum(amount);
        }

        private static void GrantStarterItems(Thing starterStorage)
        {
            if (starterStorage == null)
            {
                throw new InvalidOperationException("Underworld startup requires a starter storage chest.");
            }

            foreach (Thing reward in CreateDirectStarterRewards())
            {
                EClass.pc.AddThing(reward);
            }

            EClass.pc.ModCurrency(40, "money2");

            foreach (Thing reward in CreateStoredStarterRewards())
            {
                starterStorage.AddThing(reward);
            }
        }

        private static Point FindPlacementPoint(Point anchor, params (int x, int z)[] preferredOffsets)
        {
            foreach ((int x, int z) in preferredOffsets)
            {
                Point preferred = new Point(anchor.x + x, anchor.z + z);
                Point found = preferred.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false);
                if (found != null)
                {
                    return found;
                }
            }

            return anchor.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false, ignoreCenter: true);
        }

        private static Thing FindThingById(string id)
        {
            return EClass._map.things.FirstOrDefault(thing => thing != null && !thing.isDestroyed && thing.id == id);
        }

        private static void MoveStoryNpcOffsite(string id)
        {
            Chara chara = EClass.game?.cards?.globalCharas?.Find(id);
            if (chara == null)
            {
                return;
            }

            Zone somewhere = EClass.game.spatials.Find("somewhere");
            if (somewhere == null)
            {
                return;
            }

            if (chara.currentZone == EClass.game.StartZone || chara.currentZone == EClass._zone || chara.currentZone == null)
            {
                chara.MoveHome(somewhere);
            }
        }

        private static void RemoveSuppressedQuests()
        {
            if (EClass.game?.quests == null)
            {
                return;
            }

            QuestManager quests = EClass.game.quests;
            foreach (Quest quest in quests.list.Where(quest => quest?.source != null && SuppressedQuestIds.Contains(quest.source.id)).ToList())
            {
                quests.Remove(quest);
            }

            foreach (Quest quest in quests.globalList.Where(quest => quest?.source != null && SuppressedQuestIds.Contains(quest.source.id)).ToList())
            {
                quests.RemoveGlobal(quest);
            }
        }
    }
}
