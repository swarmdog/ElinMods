using ElinUnderworldSimulator;

public class TraitDreamPowder : TraitItemProc
{
    private static readonly string[] AllowedMealCategories =
    {
        "meal_bread",
        "meal_cookie",
        "meal_cake",
    };

    public override bool CanBlend(Thing t)
    {
        if (t == null || !t.IsFood || t.category == null)
        {
            return false;
        }

        foreach (string categoryId in AllowedMealCategories)
        {
            if (t.category.id == categoryId || t.category.IsChildOf(categoryId))
            {
                return true;
            }
        }

        return false;
    }

    public override void OnBlend(Thing t, Chara c)
    {
        UnderworldConsumptionService.Apply(new UnderworldConsumptionContext(c, owner as Thing ?? owner.Thing, UnderworldConsumptionRoute.Blend, t));
    }
}
