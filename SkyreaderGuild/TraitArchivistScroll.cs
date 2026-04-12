using SkyreaderGuild;

public class TraitArchivistScroll : TraitScroll
{
    public override void OnRead(Chara c)
    {
        Chara existing = EClass.game.cards.globalCharas.Find("srg_archivist");
        if (existing != null)
        {
            Msg.SayRaw("The Astral Archivist is already present in this world.");
            return;
        }

        Point spawnPoint = FindSummonPoint(c);
        if (spawnPoint == null || !spawnPoint.IsValid || spawnPoint.IsBlocked || spawnPoint.HasChara)
        {
            Msg.SayRaw("There isn't enough space for the summoning.");
            return;
        }

        Chara archivist = CharaGen.Create("srg_archivist", -1);
        archivist.hostility = Hostility.Neutral; // could we add some affinity with this guy so the player cn recruit easier? maybe random +10-20
        archivist.c_originalHostility = Hostility.Neutral;
        archivist.SetGlobal();
        EClass._zone.AddCard(archivist, spawnPoint);

        Msg.SayRaw("A figure materializes from streams of starlight. The Astral Archivist has arrived.");
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
