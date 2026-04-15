public class TraitUwSampleKit : TraitItem
{
    public override string LangUse => "Check the kit";

    public override bool OnUse(Chara c)
    {
        Msg.SayRaw("The pouch holds wraps and tiny bottles. Offer samples through dialogue, not by brute force.");
        return false;
    }
}
