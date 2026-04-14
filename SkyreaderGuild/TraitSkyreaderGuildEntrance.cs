using SkyreaderGuild;

public class TraitSkyreaderGuildEntrance : TraitNewZone
{
    private const int ReturnXKey = 78001;
    private const int ReturnZKey = 78002;

    public override bool AutoEnter => false;

    public override int UseDist => 1;

    public override ZoneTransition.EnterState enterState => ZoneTransition.EnterState.Center;

    public override string langOnUse => "actUse";

    public override bool TryTeleport()
    {
        Zone derphy = EClass._zone;
        if (derphy == null || derphy.id != "derphy")
        {
            SkyreaderGuild.SkyreaderGuild.Log("Skyreader guild entrance failed: active zone is not Derphy.");
            Msg.SayNothingHappen();
            return true;
        }

        Zone guild = RefZone.Get(owner.c_uidZone);
        if (guild == null || guild.destryoed || guild.id != "srg_guild_hq")
        {
            guild = derphy.FindZone("srg_guild_hq");
        }

        if (guild == null || guild.destryoed)
        {
            if (!EClass.sources.zones.map.ContainsKey("srg_guild_hq"))
            {
                SkyreaderGuild.SkyreaderGuild.Log("Skyreader guild entrance failed: SourceZone row srg_guild_hq is missing.");
                Msg.SayNothingHappen();
                return true;
            }

            guild = SpatialGen.Create("srg_guild_hq", derphy, register: true, owner.pos.x, owner.pos.z) as Zone;
            if (guild == null)
            {
                SkyreaderGuild.SkyreaderGuild.Log("Skyreader guild entrance failed: SpatialGen.Create returned null for srg_guild_hq.");
                Msg.SayNothingHappen();
                return true;
            }

            SkyreaderGuild.SkyreaderGuild.Log($"Created Skyreader Observatory as a Derphy child zone: uid={guild.uid}.");
        }

        owner.c_uidZone = guild.uid;
        guild.SetInt(ReturnXKey, owner.pos.x);
        guild.SetInt(ReturnZKey, owner.pos.z);
        Msg.SayRaw("You step through the shimmering astral gateway.");
        EClass.pc.MoveZone(guild, ZoneTransition.EnterState.Center);
        return true;
    }
}
