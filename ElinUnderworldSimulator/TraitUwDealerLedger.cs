using ElinUnderworldSimulator;

public class TraitUwDealerLedger : TraitItem
{
    public override string LangUse => "Read the ledger";

    public override bool OnUse(Chara c)
    {
        UnderworldDealService.ShowLedger();
        return false;
    }
}
