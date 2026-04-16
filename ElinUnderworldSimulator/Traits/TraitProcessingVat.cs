using System;
using ElinUnderworldSimulator;

public class TraitProcessingVat : TraitBrewery
{
    [ThreadStatic]
    private static int _pendingPotency;

    [ThreadStatic]
    private static int _pendingToxicity;

    [ThreadStatic]
    private static int _pendingTraceability;

    [ThreadStatic]
    private static int _pendingValue;

    [ThreadStatic]
    private static string _pendingInputId;

    public override int DecaySpeedChild => 350;

    public override Type type => Type.Drink;

    public override bool CanChildDecay(Card c)
    {
        return UnderworldProcessingVatService.CanProcess(c);
    }

    public override bool OnChildDecay(Card c, bool firstDecay)
    {
        Thing inputThing = c as Thing ?? c?.Thing;
        if (inputThing == null || !UnderworldProcessingVatService.CanProcess(inputThing))
        {
            return true;
        }

        if (!UnderworldProcessingVatService.IsReady(inputThing))
        {
            UnderworldProcessingVatService.HoldPendingDecay(inputThing);
            return false;
        }

        return base.OnChildDecay(c, firstDecay);
    }

    public override string GetProductID(Card c)
    {
        if (c == null)
        {
            return null;
        }

        Thing inputThing = c as Thing ?? c.Thing;
        if (inputThing != null)
        {
            _pendingPotency = inputThing.Evalue(UnderworldContentIds.PotencyElement);
            _pendingToxicity = inputThing.Evalue(UnderworldContentIds.ToxicityElement);
            _pendingTraceability = inputThing.Evalue(UnderworldContentIds.TraceabilityElement);
            _pendingValue = inputThing.GetValue();
            _pendingInputId = inputThing.id;
        }

        return UnderworldProcessingVatService.TryGetRecipe(c.id, out UnderworldVatRecipe recipe) ? recipe.OutputId : null;
    }

    public override void OnProduce(Card c)
    {
        Thing product = c as Thing ?? c?.Thing;
        try
        {
            if (product != null)
            {
                UnderworldProcessingVatService.ClearProgress(product);
                UnderworldContrabandQualityService.ApplyProcessedProduct(
                    product,
                    _pendingInputId ?? c.id,
                    _pendingPotency,
                    _pendingToxicity,
                    _pendingTraceability,
                    _pendingValue);
            }
        }
        finally
        {
            _pendingPotency = 0;
            _pendingToxicity = 0;
            _pendingTraceability = 0;
            _pendingValue = 0;
            _pendingInputId = null;
        }
    }
}
