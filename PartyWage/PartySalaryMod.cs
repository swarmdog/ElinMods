using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;

namespace PartySalaryMod
{
    [BepInPlugin("mrmeagle.elin.partywage", "Party Salary Mod", "1.4")]
    public class SalaryMod : BaseUnityPlugin
    {
        private static ConfigEntry<int> MinimumSafeMoney;
        private static ConfigEntry<float> SalaryPercentage;
        private static ConfigEntry<string> SalaryFreq;
        private static ConfigEntry<bool> NYB;

        private void Awake()
        {
            Harmony harmony = new Harmony("mrmeagle.elin.partywage");
            harmony.PatchAll();

            // Load configuration
            MinimumSafeMoney = Config.Bind(
                "General",
                "MinimumSafeMoney",
                25000,
                "The minimum amount of Orens the player must have after paying salaries."
            );

            SalaryPercentage = Config.Bind(
                "General",
                "SalaryPercentage",
                10.0f,
                "Percentage of the player's Orens used as the total salary pool (e.g., 10.0 for 10%)."
            );

            SalaryFreq = Config.Bind("General", "SalaryFreq", "monthly", "How often to pay salaries. (daily, weekly, monthly)");
            NYB = Config.Bind("General", "SalaryBonus", false, "Pay a bonus on new years.");
        } 

        public static int GetMinimumSafeMoney() => MinimumSafeMoney.Value;
        public static float GetSalaryPercentage() => SalaryPercentage.Value / 100.0f;

        public static string GetSalaryFreq() => SalaryFreq.Value;

        public static bool GetNYB() => NYB.Value;

        public static void Log(string message)
        {
            Console.WriteLine($"[PartySalaryMod]: {message}");
        }

        public static void PayPartySalaries(bool bonus=false)
        {
            Random rgen = new Random();
            Party playerParty = EClass.pc?.party;
            PartyWageStrings pws = new PartyWageStrings();
            string s_term = "salary";
            if (bonus) { s_term = "bonus"; };

            // Get the player's current Orens
            int playerOrens = EClass.pc.GetCurrency("money");
            int minimumSafeMoney = SalaryMod.GetMinimumSafeMoney();
            float salaryPercentage = SalaryMod.GetSalaryPercentage();

            // Calculate the total salary pool
            int totalSalaryPool = (int)(playerOrens * salaryPercentage);

            // Calculate individual salary share
            int partySize = playerParty.members.Count - 1;
            int individualShare = totalSalaryPool / partySize;

            // Silent if the player has no active party
            if (playerParty == null || playerParty.members == null || playerParty.members.Count < 2)
                return;

            // Ensure the player retains the minimum safe money
            if (playerOrens - totalSalaryPool < minimumSafeMoney)
            {
                Msg.SayRaw($"Having a mere ${playerOrens} on hand you decide not to distribute any ${s_term}, since it would take you below your limit of ${minimumSafeMoney}..");
                // say funny thing about not being able to afford
                EClass.pc.TalkRaw(pws.wizardPaymentExcuses[rgen.Next(pws.wizardPaymentExcuses.Length)]);
                return;
            }

            string m = pws.wizardPaymentPhrases[rgen.Next(pws.wizardPaymentPhrases.Length)];
            if (bonus)
                m = pws.wizardBonusQuips[rgen.Next(pws.wizardBonusQuips.Length)];
            EClass.pc.TalkRaw(m);
            Msg.SayRaw($"You pay a total of ${totalSalaryPool} in ${s_term} to {partySize} party members.");
            // Deduct the total salary pool from the player's Orens
            EClass.pc.ModCurrency(-totalSalaryPool, "money");
            bool squawk = false;
            // Distribute salaries to party members
            foreach (var member in playerParty.members)
            {
                if (member == null || member == EClass.pc) continue;
                member.ModCurrency(individualShare, "money"); // Add Orens to the member
                string disp_name = member.GetName(NameStyle.Full);
                if (!squawk && EClass.rnd(100) < 21) // 1 in 5 to squawk
                {
                    squawk = true;
                    member.TalkRaw(pws.rogueRemarks[rgen.Next(pws.rogueRemarks.Length)]);
                }
                SalaryMod.Log($"paid {disp_name}  $${individualShare} ");
            }


        }
    }


    [HarmonyPatch(typeof(GameDate), "AdvanceYear")]
    public static class AdvanceYearPatch
    {
        static void Postifx()
        {
            if (SalaryMod.GetNYB())
            {
                SalaryMod.PayPartySalaries(true);
            }
        }
    }

    [HarmonyPatch(typeof(GameDate), "AdvanceDay")]
    public static class AdvanceDayPatch
    {
        static void Postfix()
        {
            if(SalaryMod.GetSalaryFreq() == "daily" ||
                (SalaryMod.GetSalaryFreq() == "weekly" && EClass.player.stats.days % 7 == 0) )
            {
                SalaryMod.PayPartySalaries();
            }

        }
    }

    [HarmonyPatch(typeof(GameDate), "AdvanceMonth")]
    public static class AdvanceMonthPatch
    {
        static void Postfix()
        {
            if (SalaryMod.GetSalaryFreq() == "monthly")
            {
                SalaryMod.PayPartySalaries();
            }
            
        }

    }
}

