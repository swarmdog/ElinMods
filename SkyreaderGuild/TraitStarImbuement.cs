using SkyreaderGuild;

public class TraitStarImbuement : TraitItem
{
    public string Mode => GetParam(1) ?? "weave";

    public override bool CanStack => false;

    public override bool OnUse(Chara c)
    {
        LayerDragGrid.Create(new InvOwnerStarImbuement(owner)
        {
            mode = Mode
        });
        return false;
    }
}

public class InvOwnerStarImbuement : InvOwnerEffect
{
    public const int StarImbuementCountKey = 9002;
    public string mode = "weave";

    public override bool CanTargetAlly => true;

    public override string langTransfer => "actUse";

    public override string langWhat => "identify_what";

    public InvOwnerStarImbuement(Card owner = null, Card container = null, CurrencyType currency = CurrencyType.None)
        : base(owner, container, currency)
    {
        count = 1;
    }

    public override bool ShouldShowGuide(Thing t)
    {
        if (t == null || t.isDestroyed || t.IsLightsource || t.IsToolbelt)
        {
            return false;
        }

        if (t.GetInt(StarImbuementCountKey) >= SkyreaderGuild.SkyreaderGuild.ConfigMaxStarImbuements.Value)
        {
            return false;
        }

        if (mode == "forge")
        {
            return t.IsWeapon || t.IsRangedWeapon || t.IsThrownWeapon || t.IsAmmo
                || t.category?.IsChildOf("ring") == true || t.category?.IsChildOf("neck") == true;
        }

        return t.IsEquipment && !t.IsWeapon && !t.IsRangedWeapon && !t.IsAmmo;
    }

    public override void _OnProcess(Thing t)
    {
        Element element = t.AddEnchant(System.Math.Max(t.LV, EClass.pc.LV));
        if (element == null)
        {
            Msg.SayNothingHappen();
            return;
        }

        Msg.SayRaw($"Starlight settles into {t.Name}.");
        t.SetInt(StarImbuementCountKey, t.GetInt(StarImbuementCountKey) + 1);
        SkyreaderGuild.SkyreaderGuild.Log($"Applied star imbuement '{mode}' to {t.id}: element={element.id}, value={element.vBase + element.vSource}.");
        owner.ModNum(-1, true);
    }
}
