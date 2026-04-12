using System.Collections.Generic;
using SkyreaderGuild;

public class TraitBossScroll : TraitScroll
{
    private static readonly Dictionary<string, string> ScrollToBoss = new Dictionary<string, string>
    {
        { "srg_scroll_twilight", "srg_umbryon" },
        { "srg_scroll_radiance", "srg_solaris" },
        { "srg_scroll_abyss", "srg_erevor" },
        { "srg_scroll_nova", "srg_quasarix" },
    };

    public override void OnRead(Chara c)
    {
        string scrollId = owner.id;
        if (!ScrollToBoss.TryGetValue(scrollId, out string bossId))
        {
            SkyreaderGuild.SkyreaderGuild.Log($"Unknown boss scroll id: {scrollId}");
            Msg.SayRaw("The scroll's sigils fail to align.");
            return;
        }

        Point spawnPoint = FindSummonPoint(c);
        if (spawnPoint == null || !spawnPoint.IsValid || spawnPoint.IsBlocked || spawnPoint.HasChara)
        {
            Msg.SayRaw("There isn't enough space to summon the creature.");
            return;
        }

        Chara boss = CharaGen.Create(bossId, -1);
        boss.hostility = Hostility.Enemy;
        boss.c_originalHostility = Hostility.Enemy;
        boss.enemy = c;
        EClass._zone.AddCard(boss, spawnPoint);

        Msg.SayRaw($"The scroll crumbles to dust as {boss.Name} materializes!");
        owner.ModNum(-1, true);
    }

    private static Point FindSummonPoint(Chara c)
    {
        Point p = c.pos.GetNearestPoint(
            allowBlock: false,
            allowChara: false,
            allowInstalled: false,
            ignoreCenter: true);

        if (p != null && p.IsValid && !p.IsBlocked && !p.HasChara)
        {
            return p;
        }

        p = EClass._map.bounds.GetRandomSurface(c.pos.x, c.pos.z, 4);
        return p != null
            ? p.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false)
            : null;
    }
}
