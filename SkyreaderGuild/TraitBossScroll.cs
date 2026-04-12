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

    public override int GetActDuration(Chara c)
    {
        return 15;
    }

    private int lastStage = -1;

    private static readonly string[][] stageMessages = new string[][]
    {
        new string[] {
            "The sigils on the scroll writhe and shift. Reality bends at the edges of your vision.",
            "Ink bleeds upward from the parchment, forming shapes that should not exist.",
            "Your hands tremble as the scroll grows warm. The air thickens with cosmic pressure.",
        },
        new string[] {
            "A rift of pale light tears open beside you. Something stirs within.",
            "The ground beneath your feet vibrates. Stars appear where there should be stone.",
            "Thunder without sound shakes your bones. The summoning circle burns bright.",
        },
        new string[] {
            "A shape coalesces in the rift — vast and terrible and beautiful.",
            "The air screams silently as the entity forces its way into this plane.",
            "Gravity lurches. The creature's presence warps the fabric of the zone.",
        },
    };

    public override bool TryProgress(AIProgress p)
    {
        var custom = p as Progress_Custom;
        if (custom == null) return true;

        float progress = (float)custom.progress / custom.maxProgress;
        int stage = progress < 0.33f ? 0 : progress < 0.66f ? 1 : 2;

        if (stage != lastStage)
        {
            lastStage = stage;
            string[] pool = stageMessages[stage];
            Msg.SayRaw(pool[EClass.rnd(pool.Length)]);
        }

        return true;
    }


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
