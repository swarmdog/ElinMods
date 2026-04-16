using ElinUnderworldSimulator;

public class TraitAdvancedLab : TraitFactory
{
    public override bool Contains(RecipeSource r)
    {
        return r.idFactory == UnderworldContentIds.AdvancedLabId;
    }
}
