using SkyreaderGuild;
using UnityEngine;

public class TraitAstralExtractor : TraitItem
{
    public override bool CanStack => true;

    public override bool DisableAutoCombat => true;

    public override Emo2 GetHeldEmo(Chara c)
    {
        if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return Emo2.none;
        return TagMeteorTouchedOnCivilizedVisit.IsTouched(c) ? Emo2.hint : Emo2.none;
    }

    public override string LangUse => "Extract Starlight";

    public override bool CanUse(Chara c, Card tg)
    {
        return EClass.game.quests.IsStarted<QuestSkyreader>();
    }

    public override bool OnUse(Chara c, Card tg)
    {
        PerformExtraction(c, tg);
        return true;
    }

    public override bool CanUse(Chara c, Point p)
    {
        return EClass.game.quests.IsStarted<QuestSkyreader>();
    }

    public override bool OnUse(Chara c, Point p)
    {
        foreach (Chara chara in p.Charas) 
        {
            if (TagMeteorTouchedOnCivilizedVisit.IsTouched(chara)) { PerformExtraction(c, chara); return true; }
        }
        foreach (Thing thing in p.Things) 
        {
            if (TagMeteorTouchedOnCivilizedVisit.IsTouched(thing)) { PerformExtraction(c, thing); return true; }
        }
        
        Msg.SayRaw("There is no cosmic energy here.");
        return true;
    }

    public override bool OnUse(Chara c)
    {
        Msg.SayRaw("Hold the astral extractor and right-click a meteor-touched person or object.");
        return false;
    }


    private void PerformExtraction(Chara user, Card target)
    {
        if (user == null || target == null || target.isDestroyed) return;
        if (!TagMeteorTouchedOnCivilizedVisit.IsTouched(target))
        {
            Msg.SayRaw("The starlight has already faded.");
            return;
        }

        target.SetInt(TagMeteorTouchedOnCivilizedVisit.MeteorTouchedKey, 0);

        for (int i = 0; i < 2; i++)
        {
            user.Pick(ThingGen.Create("srg_meteorite_source"));
        }

        QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest != null)
        {
            int gpReward = CalculateGuildPoints(target);
            quest.AddGuildPoints(gpReward);
            quest.touched_cleansed++;
            SkyreaderGuild.SkyreaderGuild.Log($"Skysign extraction complete: target={target.id}, gp={gpReward}.");
        }

        RollSkysignEffect(user, target);
    }

    private static int CalculateGuildPoints(Card target)
    {
        Chara chara = target as Chara;
        if (chara != null)
        {
            return 50 + Mathf.Min(chara.LV * 5, 100);
        }

        Thing thing = target as Thing;
        if (thing != null)
        {
            return 50 + Mathf.Clamp(thing.GetValue() / 100, 0, 100);
        }

        return 50;
    }

    private static void RollSkysignEffect(Chara user, Card target)
    {
        bool isCharaTarget = target is Chara;
        int roll = EClass.rnd(isCharaTarget ? 5 : 3);

        if (roll == 0)
        {
            TriggerDimensionalGateway(target);
            return;
        }

        if (roll == 1)
        {
            TriggerAlignment(user);
            return;
        }

        if (!isCharaTarget)
        {
            TriggerAstralExposure(user, target as Thing);
            return;
        }

        Chara chara = target as Chara;
        if (roll == 2)
        {
            TriggerCosmicAttunement(chara);
        }
        else if (roll == 3)
        {
            TriggerMedicalSuccess(chara);
        }
        else
        {
            TriggerAstralExposure(user, null);
        }
    }

    private static void TriggerDimensionalGateway(Card target)
    {
        Zone rift = MeteorManager.TrySpawnAstralRift();
        if (rift == null)
        {
            Msg.SayRaw("The cosmic energies dissipate before they can take shape.");
            return;
        }

        Point portalPos = target.pos.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false, ignoreCenter: true);
        if (portalPos == null || !portalPos.IsValid)
        {
            Msg.SayRaw("A rift opens on the horizon, but no stable portal can form nearby.");
            return;
        }

        Thing portal = ThingGen.Create("srg_astral_portal");
        portal.c_uidZone = rift.uid;
        EClass._zone.AddCard(portal, portalPos).Install();
        Msg.SayRaw($"A shimmering portal materializes near {target.Name}.");
    }

    private static void TriggerAlignment(Chara user)
    {
        Msg.SayRaw("Cosmic alignment sharpens your understanding of difficult texts.");
        user.AddCondition(Condition.Create<ConBuffStats>(500, delegate(ConBuffStats con)
        {
            con.SetRefVal(74, (int)EffectId.BuffStats);
        }));
    }

    private static void TriggerCosmicAttunement(Chara target)
    {
        if (target == null) return;
        target.elements.ModBase(70, 5);
        target.elements.ModBase(71, 5);
        target.elements.ModBase(72, 5);
        target.Refresh();
        Msg.SayRaw($"{target.Name} is infused with cosmic force.");
    }

    private static void TriggerMedicalSuccess(Chara target)
    {
        if (target == null) return;
        target.ModAffinity(EClass.pc, 30);
        Msg.SayRaw($"{target.Name} looks at you with gratitude.");
    }

    private static void TriggerAstralExposure(Chara user, Thing preferredTarget)
    {
        Thing target = preferredTarget;
        if (target == null || target.isDestroyed)
        {
            System.Collections.Generic.List<Thing> candidates = new System.Collections.Generic.List<Thing>();
            foreach (Thing thing in EClass._map.things) 
            {
                if (thing == null || thing.isDestroyed) continue;
                if (user.pos.Distance(thing.pos) > 5) continue;
                if (thing.c_isImportant || thing.isMasked || thing.isHidden) continue;
                if (thing.id == "srg_astral_portal") continue;
                if (thing.trait is TraitTeleporter || thing.trait is TraitStairs) continue;
                
                candidates.Add(thing);
            }

            if (candidates.Count > 0)
            {
                target = candidates.RandomItem();
                SkyreaderGuild.SkyreaderGuild.Log($"Astral Exposure: Selected target {target.Name} ({target.id}) from {candidates.Count} candidates within radius 5.");
            }
            else
            {
                SkyreaderGuild.SkyreaderGuild.Log($"Astral Exposure: Found no valid candidates within radius 5 of {user.Name}.");
            }
        }
        else
        {
            SkyreaderGuild.SkyreaderGuild.Log($"Astral Exposure: Using preferred target {target.Name}.");
        }

        if (target == null)
        {
            Msg.SayRaw("The extractor overloads, but no object nearby can hold the change.");
            return;
        }

        string oldName = target.Name;
        int targetMatLv = Mathf.Max(1, target.LV + 5);
        SourceMaterial.Row newMaterial = MATERIAL.GetRandomMaterial(targetMatLv);
        
        SkyreaderGuild.SkyreaderGuild.Log($"Astral Exposure: Transforming {target.Name} (Material: {target.material.name}) to {newMaterial.name} (LV {targetMatLv}).");
        
        target.ChangeMaterial(newMaterial);
        Msg.SayRaw($"{oldName} shimmers and transforms into {target.Name}!");
    }
}
