using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ElinUnderworldSimulator
{
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    internal sealed class UnderworldPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> ConfigEnableOnlineFeatures;
        internal static ConfigEntry<string> ConfigUnderworldServerUrl;
        internal static UnderworldLocalServerManager LocalServerManager;
        internal static UnderworldAuthManager AuthManager;
        internal static UnderworldNetworkClient NetworkClient;
        internal static UnderworldNetworkState NetworkState;
        internal static ManualLogSource LogSource;
        internal static bool IsApplyingStartup;
        internal static bool HasAppliedStartup;
        internal static bool HasLoggedModeRegistration;
        internal static int RegisteredPrologueIndex = -1;

        private void Awake()
        {
            LogSource = Logger;
            string assemblyPath = GetType().Assembly.Location;
            string buildStamp = File.Exists(assemblyPath)
                ? File.GetLastWriteTime(assemblyPath).ToString("yyyy-MM-dd HH:mm:ss")
                : "unknown";
            Log($"Loaded plugin {ModInfo.Name} v{ModInfo.Version} from {assemblyPath} (built {buildStamp}).");

            ConfigEnableOnlineFeatures = Config.Bind("Online", "EnableOnlineFeatures", false, "Enable connection to the Underworld server.");
            ConfigUnderworldServerUrl = Config.Bind("Online", "UnderworldServerUrl", "http://127.0.0.1:8001", "URL of the bundled or self-hosted Underworld server.");
            Harmony harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll();
            UnderworldConfig.Bind(Config);
            UnderworldRuntime.Initialize();
            NetworkState = new UnderworldNetworkState();
            LocalServerManager = new UnderworldLocalServerManager(
                () => ConfigEnableOnlineFeatures != null && ConfigEnableOnlineFeatures.Value,
                () => ConfigUnderworldServerUrl.Value,
                Path.GetDirectoryName(assemblyPath) ?? string.Empty
            );
            AuthManager = new UnderworldAuthManager(() => ConfigUnderworldServerUrl.Value, LocalServerManager);
            NetworkClient = new UnderworldNetworkClient(() => ConfigUnderworldServerUrl.Value, AuthManager, NetworkState);
            LocalServerManager.InitializeOnPluginLoad();
        }

        internal static void Log(string message) => LogSource?.LogInfo(message);

        internal static void Warn(string message) => LogSource?.LogWarning(message);

        internal static void Error(string message, Exception ex = null)
        {
            LogSource?.LogError(ex == null ? message : message + Environment.NewLine + ex);
        }

        internal static bool IsOnlineReady()
        {
            return ConfigEnableOnlineFeatures != null
                && ConfigEnableOnlineFeatures.Value
                && NetworkClient != null;
        }

        internal static string GetPlayerDisplayName()
        {
            return EClass.pc?.NameTitled ?? EClass.pc?.Name ?? "Unknown Operator";
        }

        internal static string GetOnlineStatusText(string fallback)
        {
            string localStatus = LocalServerManager?.GetStatusMessage();
            return string.IsNullOrEmpty(localStatus) ? fallback : localStatus;
        }

        internal static string GetOnlineFailureMessage(Exception ex, string fallback)
        {
            string localStatus = LocalServerManager?.GetStatusMessage();
            if (!string.IsNullOrEmpty(localStatus))
            {
                return localStatus;
            }

            if (ex != null && !string.IsNullOrEmpty(ex.Message))
            {
                return ex.Message;
            }

            return fallback;
        }

        internal static bool IsUnderworldMode()
        {
            return EClass.game != null
                && RegisteredPrologueIndex >= 0
                && EClass.game.idPrologue == RegisteredPrologueIndex;
        }

        private void OnApplicationQuit()
        {
            LocalServerManager?.Shutdown();
        }

        private void OnDestroy()
        {
            LocalServerManager?.Shutdown();
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
            if (!UnderworldPlugin.IsUnderworldMode())
            {
                return;
            }

            if (UnderworldPlugin.HasAppliedStartup)
            {
                UnderworldPlugin.Warn("Startup bootstrap already applied.");
                return;
            }

            UnderworldPlugin.IsApplyingStartup = true;
            UnderworldPlugin.Log("Applying underworld startup bootstrap.");

            try
            {
                UnderworldBootstrap.Apply();
                UnderworldPlugin.HasAppliedStartup = true;
                UnderworldPlugin.Log("Underworld startup bootstrap complete.");
            }
            catch (Exception ex)
            {
                UnderworldPlugin.HasAppliedStartup = false;
                UnderworldPlugin.Error("Underworld startup failed.", ex);
            }
            finally
            {
                UnderworldPlugin.IsApplyingStartup = false;
            }
        }
    }

    [HarmonyPatch(typeof(GameDate), nameof(GameDate.AdvanceDay))]
    internal static class Patch_GameDate_AdvanceDay
    {
        private static void Postfix()
        {
            if (!UnderworldPlugin.IsUnderworldMode())
            {
                return;
            }

            UnderworldBootstrap.MaintainStoryIsolation();
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
            if (UnderworldPlugin.IsOnlineReady())
            {
                UnderworldPlugin.LocalServerManager?.EnsureBootstrapStarted();
            }
        }
    }

    [HarmonyPatch(typeof(DramaCustomSequence), nameof(DramaCustomSequence.Build))]
    internal static class Patch_DramaCustomSequence_Build
    {
        private static void Postfix(DramaCustomSequence __instance, Chara c)
        {
            if (UnderworldPlugin.IsUnderworldMode()
                && c != null
                && c.id == ModInfo.FixerId
                && EClass._zone.IsUserZone)
            {
                __instance.Choice2(c.trait.TextNextRestock, "_buy").DisableSound();
            }

            UnderworldDealService.AppendChoices(__instance, c);
        }
    }

    [HarmonyPatch(typeof(Trait), nameof(Trait.OnBarter))]
    internal static class Patch_Trait_OnBarter
    {
        private static void Postfix(Trait __instance)
        {
            UnderworldFixerShopService.TryPopulate(__instance);
        }
    }
}
