using ElinUnderworldSimulator;

public class TraitUwAntidoteVial : TraitItem
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
        Msg.SayRaw("Use the antidote on someone spiraling from a bad handoff.");
        return false;
    }
}
