using SkyreaderGuild;
using UnityEngine;

public class TraitMeteorCore : TraitItem
{
    public override bool CanStack => false;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.Log("Player interacting with meteor core.");

        int sourceCount = 1 + EClass.rnd(4);
        for (int i = 0; i < sourceCount; i++)
        {
            c.Pick(ThingGen.Create("srg_meteorite_source"));
        }

        int treasureCount = 2 + EClass.rnd(3);
        for (int i = 0; i < treasureCount; i++)
        {
            Thing t = ThingGen.CreateFromCategory("junk", EClass._zone.DangerLv);
            c.Pick(t);
        }

        QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest != null)
        {
            quest.meteors_found++;
            quest.AddGuildPoints(100 + EClass.rnd(51));
        }

        Msg.SayRaw("You extract fragments from the meteor core. The starlight dims.");
        RollPostEvent(c);
        owner.ModNum(-1, true);
        return true;
    }

    private static void RollPostEvent(Chara c)
    {
        int roll = EClass.rnd(100);

        if (roll < 25)
        {
            Msg.SayRaw("Insight floods your mind. Extra fragments gather in your hands.");
            int bonus = 1 + EClass.rnd(3);
            for (int i = 0; i < bonus; i++)
            {
                c.Pick(ThingGen.Create("srg_meteorite_source"));
            }
            return;
        }

        if (roll < 50)
        {
            Msg.SayRaw("The meteor's energy lashes out. Something emerges from the fragments.");
            Chara boss = EClass._zone.SpawnMob(null, SpawnSetting.Boss(EClass._zone.DangerLv, EClass._zone.DangerLv));
            if (boss != null)
            {
                boss.hostility = Hostility.Enemy;
                boss.c_originalHostility = Hostility.Enemy;
            }
            for (int i = 0; i < 2 + EClass.rnd(2); i++)
            {
                EClass._zone.SpawnMob();
            }
            return;
        }

        if (roll < 75)
        {
            Msg.SayRaw("Reality shimmers. The ground becomes unstable.");
            for (int i = 0; i < 3 + EClass.rnd(3); i++)
            {
                Point p = EClass._map.bounds.GetRandomSurface(c.pos.x, c.pos.z, 5);
                if (p != null && !p.HasThing)
                {
                    Thing trap = ThingGen.Create("trap_mine");
                    trap.SetHidden();
                    EClass._zone.AddCard(trap, p).Install();
                }
            }
            return;
        }

        Msg.SayRaw("A group of mercenaries emerges, drawn by the meteor's energy.");
        for (int i = 0; i < 3 + EClass.rnd(3); i++)
        {
            Chara merc = EClass._zone.SpawnMob();
            if (merc != null)
            {
                merc.hostility = Hostility.Enemy;
                merc.c_originalHostility = Hostility.Enemy;
            }
        }
    }
}
