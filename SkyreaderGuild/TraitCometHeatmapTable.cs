using SkyreaderGuild;

public class TraitCometHeatmapTable : TraitItem
{
    public override string LangUse => "View Astral Contamination Heatmap";

    public override bool CanUse(Chara c) => true;

    public override bool OnUse(Chara c)
    {
        SkyreaderGuild.SkyreaderGuild.ShowCometHeatmap();
        return true;
    }
}
