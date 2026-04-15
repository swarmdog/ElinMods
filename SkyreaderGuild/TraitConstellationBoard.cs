using SkyreaderGuild;

public class TraitConstellationBoard : TraitItem
{
    public override string LangUse => "View Constellations";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowConstellationBoard();
        return true;
    }
}
