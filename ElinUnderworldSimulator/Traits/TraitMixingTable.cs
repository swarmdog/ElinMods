using ElinUnderworldSimulator;

public class TraitMixingTable : TraitFactory
{
    public override bool Contains(RecipeSource r)
    {
        return r.idFactory == UnderworldContentIds.MixingTableId;
    }
}
