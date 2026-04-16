using ElinUnderworldSimulator;

public class TraitUnderworldFixer : TraitUniqueMerchant
{
    public override ShopType ShopType => ShopType.Specific;

    public override CurrencyType CurrencyType => CurrencyType.Money2;

    public override string LangBarter => "daBuyStarter";

    public override bool CanInvite
    {
        get
        {
            int rank = UnderworldPlugin.NetworkState?.PlayerStatus?.UnderworldRank ?? 0;
            return rank >= UnderworldConfig.FixerRecruitMinRank.Value;
        }
    }
}
