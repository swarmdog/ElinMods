using SkyreaderGuild;

public class TraitSkyreaderGuildExit : TraitNewZone
{
    private const int ReturnXKey = 78001;
    private const int ReturnZKey = 78002;

    public override bool AutoEnter => false;

    public override int UseDist => 1;

    public override string langOnUse => "stairsUp";

    public override bool TryTeleport()
    {
        Zone guild = EClass._zone;
        Zone parent = EClass._zone?.parent as Zone;
        if (parent == null)
        {
            SkyreaderGuild.SkyreaderGuild.Log("Skyreader guild exit failed: active zone has no parent zone.");
            Msg.SayNothingHappen();
            return true;
        }

        ZoneTransition transition = new ZoneTransition
        {
            state = ZoneTransition.EnterState.Center,
        };

        Point entrancePos = FindDerphyEntrance(parent);
        if (entrancePos != null)
        {
            transition.state = ZoneTransition.EnterState.Exact;
            transition.x = entrancePos.x;
            transition.z = entrancePos.z;
        }
        else if (guild != null && guild.GetInt(ReturnXKey) != 0)
        {
            transition.state = ZoneTransition.EnterState.Exact;
            transition.x = guild.GetInt(ReturnXKey);
            transition.z = guild.GetInt(ReturnZKey);
        }
        else
        {
            SkyreaderGuild.SkyreaderGuild.Log("Skyreader guild exit could not find Derphy entrance coordinates; returning to Derphy center.");
        }

        Msg.SayRaw("You step back through the astral gateway.");
        EClass.pc.MoveZone(parent, transition);
        return true;
    }

    private static Point FindDerphyEntrance(Zone parent)
    {
        if (parent?.map == null) return null;

        foreach (Thing thing in parent.map.things)
        {
            if (thing != null && !thing.isDestroyed && thing.id == "srg_guild_entrance")
            {
                return thing.pos.Copy();
            }
        }

        return null;
    }
}
