using SkyreaderGuild;

public class TraitAstrologicalCodex : TraitWorkbench
{
    public override string IDReqEle(RecipeSource r)
    {
        return "reading";
    }

    public override bool Contains(RecipeSource r)
    {
        if (!base.Contains(r))
        {
            return false;
        }

        QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest == null)
        {
            return false;
        }

        GuildRank rank = quest.GetCurrentRank();
        switch (r.id)
        {
            case "srg_scroll_twilight":
            case "srg_scroll_radiance":
            case "srg_scroll_abyss":
            case "srg_scroll_nova":
                return rank >= GuildRank.CosmosApplied;
            case "srg_scroll_convergence":
                return rank >= GuildRank.Understander;
            case "srg_weave_stars":
            case "srg_starforge":
                return rank >= GuildRank.Seeker;
            default:
                return true;
        }
    }
}
