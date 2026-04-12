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

        return quest.CanUseRecipe(r.id);
    }
}
