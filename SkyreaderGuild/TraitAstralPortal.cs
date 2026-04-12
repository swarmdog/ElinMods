using SkyreaderGuild;

public class TraitAstralPortal : TraitNewZone
{
    public override bool IsTeleport => true;

    public override bool AutoEnter => true;

    public override int UseDist => 1;
    // these rift portals will get cleaned up by town reset but we could consider despawning them after a single use.  Not urgent
    public override bool TryTeleport()
    {
        Zone rift = RefZone.Get(owner.c_uidZone);
        if (rift == null || rift.destryoed)
        {
            Msg.SayRaw("The portal flickers and fades. The rift has closed.");
            SkyreaderGuild.SkyreaderGuild.Log("Astral portal self-destructing: linked rift is missing.");
            owner.Destroy();
            return true;
        }

        Msg.SayRaw("You step through the shimmering portal.");
        EClass.pc.MoveZone(rift, ZoneTransition.EnterState.Teleport);
        return true;
    }

    public override void OnChangePlaceState(PlaceState state)
    {
        if (state != PlaceState.installed) return;

        Zone rift = RefZone.Get(owner.c_uidZone);
        if (rift == null || rift.destryoed)
        {
            SkyreaderGuild.SkyreaderGuild.Log("Astral portal removed on install: linked rift is missing.");
            owner.Destroy();
        }
    }
}
