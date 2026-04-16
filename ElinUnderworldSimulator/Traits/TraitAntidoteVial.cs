using ElinUnderworldSimulator;

public class TraitAntidoteVial : TraitItem
{
    public override string LangUse => "Use antidote";

    public override bool CanUse(Chara c, Card tg)
    {
        return tg is Chara;
    }

    public override bool OnUse(Chara c, Card tg)
    {
        if (!UnderworldDealService.TryUseAntidoteOn(tg))
        {
            return false;
        }

        owner.ModNum(-1);
        return true;
    }

    public override bool OnUse(Chara c)
    {
        int addictionBefore = UnderworldRuntime.Data.PlayerAddiction;
        UnderworldRuntime.ReducePlayerAddiction(20);
        c.RemoveCondition<ConUWWithdrawal>();

        if (addictionBefore == UnderworldRuntime.Data.PlayerAddiction)
        {
            Msg.SayRaw("You do not need the reprieve right now.");
            return false;
        }

        Msg.SayRaw("The reprieve scrubs the edge off the craving.");
        owner.ModNum(-1);
        return true;
    }
}
