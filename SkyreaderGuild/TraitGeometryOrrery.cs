using SkyreaderGuild;

public class TraitGeometryOrrery : TraitItem
{
    public override string LangUse => "Study Rift Geometry";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowGeometryOrrery();
        return true;
    }
}
