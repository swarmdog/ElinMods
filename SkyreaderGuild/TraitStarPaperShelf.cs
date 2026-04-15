using SkyreaderGuild;

public class TraitStarPaperShelf : TraitItem
{
    public override string LangUse => "Read Star Papers";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowStarPaperShelf();
        return true;
    }
}
