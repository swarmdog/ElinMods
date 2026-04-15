using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ElinUnderworldSimulator
{
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    internal sealed class UnderworldPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource LogSource;
        internal static bool IsApplyingStartup;
        internal static bool HasAppliedStartup;
        internal static bool HasLoggedModeRegistration;
        internal static int RegisteredPrologueIndex = -1;

        private void Awake()
        {
            LogSource = Logger;
            Harmony harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();
            UnderworldRuntime.Initialize();
            Log($"Loaded plugin {ModInfo.Name} v{ModInfo.Version}.");
        }

        internal static void Log(string message) => LogSource?.LogInfo(message);

        internal static void Warn(string message) => LogSource?.LogWarning(message);

        internal static void Error(string message, Exception ex = null)
        {
            LogSource?.LogError(ex == null ? message : message + Environment.NewLine + ex);
        }

        internal static int EnsureUnderworldPrologueRegistered()
        {
            if (RegisteredPrologueIndex >= 0
                && EClass.setting?.start?.prologues != null
                && RegisteredPrologueIndex < EClass.setting.start.prologues.Count)
            {
                return RegisteredPrologueIndex;
            }

            if (EClass.setting?.start?.prologues == null || EClass.setting.start.prologues.Count == 0)
            {
                Warn("Unable to register Underworld startup because the base prologue list is unavailable.");
                return -1;
            }

            Prologue template = EClass.setting.start.prologues[0];
            EClass.setting.start.prologues.Add(new Prologue
            {
                type = template.type,
                idStartZone = template.idStartZone,
                startX = template.startX,
                startZ = template.startZ,
                year = template.year,
                month = template.month,
                day = template.day,
                hour = template.hour,
                posAsh = template.posAsh,
                posFiama = template.posFiama,
                posPunk = template.posPunk,
                weather = template.weather,
            });
            RegisteredPrologueIndex = EClass.setting.start.prologues.Count - 1;
            Log($"Registered underworld prologue at index {RegisteredPrologueIndex}.");
            return RegisteredPrologueIndex;
        }
    }

    [HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.SetChara))]
    internal static class Patch_UICharaMaker_SetChara
    {
        private static void Postfix(UICharaMaker __instance)
        {
            int underworldIndex = UnderworldPlugin.EnsureUnderworldPrologueRegistered();
            if (underworldIndex < 0)
            {
                return;
            }

            var modes = new System.Collections.Generic.List<string>(__instance.listMode);
            if (!modes.Contains(ModInfo.StartupLabel))
            {
                modes.Add(ModInfo.StartupLabel);
                __instance.listMode = modes.ToArray();
                if (!UnderworldPlugin.HasLoggedModeRegistration)
                {
                    UnderworldPlugin.HasLoggedModeRegistration = true;
                    UnderworldPlugin.Log($"Registered startup mode '{ModInfo.StartupLabel}'.");
                }
            }

            if (EMono.game.idPrologue == underworldIndex)
            {
                __instance.textMode.SetText(ModInfo.StartupLabel);
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.StartNewGame))]
    internal static class Patch_Game_StartNewGame
    {
        private static void Prefix()
        {
            UnderworldPlugin.HasAppliedStartup = false;
        }

        private static void Postfix()
        {
            if (EClass.game.idPrologue != UnderworldPlugin.RegisteredPrologueIndex)
            {
                return;
            }

            if (UnderworldPlugin.HasAppliedStartup)
            {
                UnderworldPlugin.Warn("Startup bootstrap already applied.");
                return;
            }

            UnderworldPlugin.HasAppliedStartup = true;
            UnderworldPlugin.IsApplyingStartup = true;
            UnderworldPlugin.Log("Applying underworld startup bootstrap.");

            try
            {
                UnderworldBootstrap.Apply();
                UnderworldPlugin.Log("Underworld startup bootstrap complete.");
            }
            catch (Exception ex)
            {
                UnderworldPlugin.Error("Underworld startup failed.", ex);
            }
            finally
            {
                UnderworldPlugin.IsApplyingStartup = false;
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.OnBeforeSave))]
    internal static class Patch_Game_OnBeforeSave
    {
        private static void Postfix()
        {
            UnderworldRuntime.Save();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Load))]
    internal static class Patch_Game_Load
    {
        private static void Postfix()
        {
            UnderworldRuntime.Load();
        }
    }

    [HarmonyPatch(typeof(DramaCustomSequence), nameof(DramaCustomSequence.Build))]
    internal static class Patch_DramaCustomSequence_Build
    {
        private static void Postfix(DramaCustomSequence __instance, Chara c)
        {
            UnderworldDealService.AppendChoices(__instance, c);
        }
    }
}
