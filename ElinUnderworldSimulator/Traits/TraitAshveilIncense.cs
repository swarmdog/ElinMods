using ElinUnderworldSimulator;

public class TraitAshveilIncense : TraitItemProc
{
    public override bool IsThrowMainAction => true;

    public override ThrowType ThrowType => ThrowType.Potion;

    public override void OnThrowGround(Chara c, Point p)
    {
        UnderworldConsumptionService.Apply(new UnderworldConsumptionContext(c, owner as Thing ?? owner.Thing, UnderworldConsumptionRoute.Throw, groundPoint: p));
        owner.ModNum(-1);
    }
}
