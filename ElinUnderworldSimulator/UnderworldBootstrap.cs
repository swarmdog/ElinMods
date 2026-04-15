using System;

namespace ElinUnderworldSimulator
{
    internal static class UnderworldBootstrap
    {
        internal static void Apply()
        {
            RequireStartZone();
            UnderworldRuntime.ResetForNewGame();

            if (!EClass._zone.IsPCFaction)
            {
                EClass._zone.ClaimZone();
            }

            EnsureFixtures();
            GrantStarterItems();
            UnderworldPlugin.Log("Underworld startup applied.");
        }

        private static void RequireStartZone()
        {
            if (EClass._zone != EClass.game.StartZone)
            {
                throw new InvalidOperationException("Underworld startup must run inside the start zone.");
            }
        }

        private static void EnsureFixtures()
        {
            PlaceInstalledThing(ModInfo.MixingTableId, 2, 1);
            PlaceInstalledThing(ModInfo.ContrabandChestId, -2, 1);
            EnsureFixer();
        }

        private static void EnsureFixer()
        {
            foreach (Chara chara in EClass._map.charas)
            {
                if (chara != null && !chara.isDestroyed && chara.id == ModInfo.FixerId)
                {
                    return;
                }
            }

            Chara fixer = CharaGen.Create(ModInfo.FixerId);
            fixer.SetHomeZone(EClass._zone);
            Point point = EClass.pc.pos.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false, ignoreCenter: true)
                ?? new Point(EClass.pc.pos.x + 1, EClass.pc.pos.z + 2);

            fixer = EClass._zone.AddCard(fixer, point).Chara;
            if (EClass.Branch != null)
            {
                EClass.Branch.AddMemeber(fixer);
            }
        }

        private static void PlaceInstalledThing(string id, int offsetX, int offsetZ)
        {
            foreach (Thing thing in EClass._map.things)
            {
                if (thing != null && !thing.isDestroyed && thing.id == id)
                {
                    return;
                }
            }

            Point desired = new Point(EClass.pc.pos.x + offsetX, EClass.pc.pos.z + offsetZ)
                .GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false)
                ?? EClass.pc.pos.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false);

            Thing thingToPlace = ThingGen.Create(id);
            thingToPlace = EClass._zone.AddCard(thingToPlace, desired).Thing;
            thingToPlace.Install();
        }

        private static void GrantStarterItems()
        {
            Grant(ModInfo.DealerLedgerId, 1);
            Grant(ModInfo.SampleKitId, 1);
            Grant(ModInfo.AntidoteId, 2);

            Grant("uw_whispervine", 6);
            Grant("uw_dreamblossom", 6);
            Grant("uw_shadowcap", 5);
            Grant("uw_crude_moonite", 4);
            Grant("potion_empty", 6);
            Grant("money2", 30);
        }

        private static void Grant(string id, int amount)
        {
            EClass.player.DropReward(ThingGen.Create(id).SetNum(amount));
        }
    }
}
