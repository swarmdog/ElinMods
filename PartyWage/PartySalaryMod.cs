using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.ComponentModel;
using System.Text.Json;

namespace PartySalaryMod
{
    [BepInPlugin("mrmeagle.elin.partywage", "Party Salary Mod", "1.3")]
    public class SalaryMod : BaseUnityPlugin
    {
        private static ConfigEntry<int> MinimumSafeMoney;
        private static ConfigEntry<float> SalaryPercentage;

        private void Awake()
        {
            Harmony harmony = new Harmony("mrmeagle.elin.partywage");
            harmony.PatchAll();

            // Load configuration
            MinimumSafeMoney = Config.Bind(
                "General",
                "MinimumSafeMoney",
                5000,
                "The minimum amount of Orens the player must have after paying salaries."
            );

            SalaryPercentage = Config.Bind(
                "General",
                "SalaryPercentage",
                10.0f,
                "Percentage of the player's Orens used as the total salary pool (e.g., 10.0 for 10%)."
            );
        }

        public static int GetMinimumSafeMoney() => MinimumSafeMoney.Value;
        public static float GetSalaryPercentage() => SalaryPercentage.Value / 100.0f;


        public static void Log(string message)
        {
            Console.WriteLine($"[PartySalaryMod]: {message}");
        }
    }

    [HarmonyPatch(typeof(GameDate), "AdvanceMonth")]
    public static class AdvanceMonthPatch
    {
        static void Postfix()
        {
            PayPartySalaries();
            
        }
        private static void PayPartySalaries()
        {
            Party playerParty = EClass.pc?.party;

            // Get the player's current Orens
            int playerOrens = EClass.pc.GetCurrency("money");
            int minimumSafeMoney = SalaryMod.GetMinimumSafeMoney();
            float salaryPercentage = SalaryMod.GetSalaryPercentage();

            // Calculate the total salary pool
            int totalSalaryPool = (int)(playerOrens * salaryPercentage);

            // Calculate individual salary share
            int partySize = playerParty.members.Count - 1;
            int individualShare = totalSalaryPool / partySize;

            //SalaryMod.Log($"party salary mod notes a day has passed with {partySize} members in ur party and a wallet of {playerOrens}");
            //SalaryMod.Log($"config says {minimumSafeMoney} min safe money and {totalSalaryPool} salary pool");

            // Silent if the player has no active party
            if (playerParty == null || playerParty.members == null || playerParty.members.Count < 2)
                return;

            // Ensure the player retains the minimum safe money
            if (playerOrens - totalSalaryPool < minimumSafeMoney)
            {
                Msg.SayRaw($"Having a mere ${playerOrens} on hand you decide not to distribute salaries this month, since it would take you below your limit of ${minimumSafeMoney}..");
                // say funny thing about not being able to afford
                return;
            }

            Msg.SayRaw($"You pay a total of ${totalSalaryPool} to {partySize} party members:");
            // Distribute salaries to party members
            foreach (var member in playerParty.members)
            {
                if (member == null || member == EClass.pc) continue;
                member.ModCurrency(individualShare, "money"); // Add Orens to the member
                string disp_name = member.GetName(NameStyle.Full);
                SalaryMod.Log($"paid {disp_name}  $${individualShare} ");
                Msg.SayRaw($"{disp_name} - ${individualShare}");
            }
            
            // Deduct the total salary pool from the player's Orens
            EClass.pc.ModCurrency(-totalSalaryPool, "money");

        }

    }
}

