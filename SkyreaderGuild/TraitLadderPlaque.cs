using SkyreaderGuild;

public class TraitLadderPlaque : TraitItem
{
    public override string LangUse => "Read Starlight Ladder";

    public override bool CanUse(Chara c)
    {
        return true;
    }

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowLadderPlaque();
        return true;
    }
}
