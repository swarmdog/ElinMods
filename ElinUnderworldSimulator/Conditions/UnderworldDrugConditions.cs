using System;
using System.Collections.Generic;
using ElinUnderworldSimulator;

public abstract class UnderworldDrugCondition : Condition
{
    public override int GetPhase() => 0;

    public override bool ShouldOverride(Condition c)
    {
        return true;
    }

    protected float ConditionScale => Math.Max(0.1f, power / 100f);

    protected int CurrentPotency => Math.Max(0, refVal2);

    protected int ResolveDuration(int fallbackDuration)
    {
        return refVal > 0 ? refVal : fallbackDuration;
    }

    protected int ScaledStatValue(int baseValue)
    {
        if (baseValue == 0)
        {
            return 0;
        }

        int sign = Math.Sign(baseValue);
        return sign * Math.Max(1, (int)Math.Round(Math.Abs(baseValue) * ConditionScale));
    }

    protected int ScaleCrashDuration(int baseValue)
    {
        return Math.Max(1, (int)Math.Round(baseValue * UnderworldConfig.CrashSeverityMultiplier.Value));
    }

    public override void SetOwner(Chara _owner, bool onDeserialize = false)
    {
        base.SetOwner(_owner, onDeserialize);
        if (elements == null)
        {
            return;
        }

        List<Element> snapshot = new List<Element>(elements.dict.Values);
        foreach (Element element in snapshot)
        {
            elements.SetBase(element.id, ScaledStatValue(element.ValueWithoutLink));
        }
        elements.SetParent(owner);
    }
}

public abstract class UnderworldCrashCondition : BadCondition
{
    public override int GetPhase() => 0;

    public override bool ShouldOverride(Condition c)
    {
        return true;
    }

    protected int ScaleCrashDuration(int baseValue)
    {
        return Math.Max(1, (int)Math.Round(baseValue * UnderworldConfig.CrashSeverityMultiplier.Value));
    }
}

public class ConUWWhisperHigh : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(120);
    }

    public override void OnRemoved()
    {
        if (EClass.rnd(10) == 0)
        {
            owner.AddCondition<ConDim>(20, force: true);
        }
    }
}

public class ConUWShadowRush : UnderworldDrugCondition
{
    private int tickCounter;

    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(95);
    }

    public override void Tick()
    {
        base.Tick();
        tickCounter++;
        int cadence = CurrentPotency >= 90 ? 2 : 3;
        if (tickCounter % cadence == 0)
        {
            AM_Adv.actCount++;
            owner.ModExp(78, 50);
        }
    }

    public override void OnRemoved()
    {
        owner.AddCondition<ConUWShadowCrash>(ScaleCrashDuration(30), force: true);
    }
}

public class ConUWShadowCrash : UnderworldCrashCondition
{
    public override void Tick()
    {
        base.Tick();
        owner.stamina.Mod(-2);
    }
}

public class ConUWDreamHigh : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(170);
    }

    public override void OnStart()
    {
        if (CurrentPotency >= 70 && EClass.rnd(100) < 15)
        {
            owner.AddCondition<ConHallucination>(30, force: true);
        }
    }
}

public class ConUWVoidRage : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(206);
    }

    public override void OnRemoved()
    {
        if (EClass.rnd(5) == 0)
        {
            owner.AddCondition<ConConfuse>(100, force: true);
        }
    }
}

public class ConUWCrimsonSurge : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(232);
    }

    public override void OnStart()
    {
        owner.ModTempElement(60, ScaledStatValue(50), naturalDecay: false, onlyRenew: true);
        owner.HealHP(15 + ScaledStatValue(10));
    }

    public override void OnRemoved()
    {
        if (owner.hp > owner.MaxHP)
        {
            owner.hp = owner.MaxHP;
        }
    }
}

public class ConUWWhisperCalm : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(54);
    }

    public override void Tick()
    {
        base.Tick();
        if (EClass.rnd(2) == 0)
        {
            owner.sleepiness.Mod(-1);
        }
    }
}

public class ConUWDreamCalm : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(70);
    }

    public override void OnStart()
    {
        if (CurrentPotency >= 60 && EClass.rnd(10) == 0)
        {
            owner.AddCondition<ConHallucination>(30, force: true);
        }
    }
}

public class ConUWBerserkerRage : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(136);
    }

    public override void OnStart()
    {
        owner.ModTempElement(60, ScaledStatValue(30), naturalDecay: false, onlyRenew: true);
    }

    public override void OnRemoved()
    {
        owner.AddCondition<ConUWBerserkerCrash>(ScaleCrashDuration(15), force: true);
    }
}

public class ConUWBerserkerCrash : UnderworldCrashCondition
{
    public override void OnStart()
    {
        owner.AddCondition<ConConfuse>(ScaleCrashDuration(15), force: true);
    }
}

public class ConUWShadowRushX : UnderworldDrugCondition
{
    private int tickCounter;

    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(67);
    }

    public override void Tick()
    {
        base.Tick();
        tickCounter++;
        if (tickCounter % 2 == 0)
        {
            AM_Adv.actCount++;
        }
    }

    public override void OnRemoved()
    {
        owner.AddCondition<ConUWRushCrash>(ScaleCrashDuration(40), force: true);
    }
}

public class ConUWRushCrash : UnderworldCrashCondition
{
    public override void Tick()
    {
        base.Tick();
        owner.stamina.Mod(-3);
    }

    public override void OnStart()
    {
        owner.AddCondition<ConDim>(ScaleCrashDuration(40), force: true);
    }
}

public class ConUWFrostbloom : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(216);
    }

    public override void Tick()
    {
        base.Tick();
        if (owner.hp < owner.MaxHP)
        {
            owner.HealHP(ScaledStatValue(3));
        }
    }
}

public class ConUWAshveil : UnderworldDrugCondition
{
    public override int EvaluateTurn(int p)
    {
        return ResolveDuration(116);
    }

    public override void OnStart()
    {
        owner.AddCondition<ConSeeInvisible>(ResolveDuration(116), force: true);
    }
}

public class ConUWWithdrawal : UnderworldCrashCondition
{
    public override bool UseElements => true;

    public override bool PreventRegen => true;

    public override int GetPhase()
    {
        return Math.Max(0, Math.Min(2, refVal));
    }

    public override int EvaluateTurn(int p)
    {
        return 999999;
    }

    public override void OnChangePhase(int lastPhase, int newPhase)
    {
        elements.SetBase(70, 0);
        elements.SetBase(77, 0);
        elements.SetBase(79, 0);

        switch (newPhase)
        {
            case 0:
                elements.SetBase(70, -5);
                break;
            case 1:
                elements.SetBase(70, -10);
                elements.SetBase(77, -5);
                break;
            default:
                elements.SetBase(70, -15);
                elements.SetBase(77, -10);
                elements.SetBase(79, -5);
                break;
        }
    }

    public override void Tick()
    {
        if (refVal >= 2 && EClass.rnd(100) == 0)
        {
            owner.Vomit();
        }
    }
}

public class ConUWOverdose : UnderworldCrashCondition
{
    public override Emo2 EmoIcon => Emo2.poison;

    public override bool UseElements => true;

    public override bool PreventRegen => true;

    public override int GetPhase()
    {
        return Math.Max(0, Math.Min(2, refVal));
    }

    public override int EvaluateTurn(int p)
    {
        return Math.Max(60, p);
    }

    public override void OnChangePhase(int lastPhase, int newPhase)
    {
        elements.SetBase(70, 0);
        elements.SetBase(77, 0);
        elements.SetBase(79, 0);

        switch (newPhase)
        {
            case 0:
                elements.SetBase(70, -10);
                elements.SetBase(77, -5);
                break;
            case 1:
                elements.SetBase(70, -20);
                elements.SetBase(77, -10);
                elements.SetBase(79, -10);
                break;
            default:
                elements.SetBase(70, -30);
                elements.SetBase(77, -15);
                elements.SetBase(79, -15);
                owner.AddCondition<ConParalyze>(50, force: true);
                break;
        }
    }

    public override void Tick()
    {
        if (EClass.rnd(10) == 0)
        {
            Mod(-1);
        }

        if (refVal >= 2 && EClass.rnd(50) == 0)
        {
            owner.Vomit();
        }
    }
}
