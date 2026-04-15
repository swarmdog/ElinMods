using SkyreaderGuild;

public class TraitStarPaperWritingDesk : TraitItem
{
    public override string LangUse => "Write Star Paper";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowStarPaperDesk();
        return true;
    }
}
