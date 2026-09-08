using BepInEx.Bootstrap;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;

namespace Manimal.LoadAmmoAnim.Patches
{
    // compat shim for ContinuousLoadAmmo (com.ozen.continuousloadammo).
    // CLA listens for OnHandsControllerChanged and cancels its loading session whenever
    // the hands controller swaps to anything other than empty hands. our Proceed swaps
    // to LoadAmmoBundleController which trips that listeneri b these patches keep CLA
    // from killing our session and prevent it from yanking our bundle mid-load.
    internal static class ContinuousLoadAmmoCompat
    {
        private const string ClaGuid = "com.ozen.continuousloadammo";

        public static bool IsInstalled =>
            Chainloader.PluginInfos.ContainsKey(ClaGuid);

        public static void EnablePatches()
        {
            Plugin.LogSource.LogInfo("[LoadAmmoAnim] ContinuousLoadAmmo detected, enabling compat patches.");
            new ClaSetEmptyHandsPatch().Enable();
            new ClaStopOnHandsChangePatch().Enable();
            new ClaTrySetLastEquippedWeaponPatch().Enable();
        }
    }

    // CLA calls SetEmptyHands(null) to put the gun away before its loading anim starts.
    // while were actively loading we block it so it cant race our Proceed.
    public class ClaSetEmptyHandsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(Player), "SetEmptyHands",
                new[] { typeof(Callback<IEmptyHandsController>) });

        [PatchPrefix]
        public static bool Prefix(Player __instance)
        {
            if (!__instance.IsYourPlayer) return true;
            var session = LoadAmmoAnimState.TryGet(__instance);
            if (session != null && session.IsLoading) return false;
            return true;
        }
    }

    // suppresses CLA's StopLoadingOnHandsChange while our anim is active. without
    // this, it fires every time we Proceed into our bundle controller and kills
    // the session after bullet 1.
    public class ClaStopOnHandsChangePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // CLA lives in a different assembly, so we look it up at runtime instead
            // of taking a hard reference to it.
            var claType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "ContinuousLoadAmmo.Controllers.LoadAmmoController");

            if (claType == null)
            {
                Plugin.LogSource.LogWarning("[LoadAmmoAnim] CLA compat: could not find LoadAmmoController type.");
                return null;
            }

            var method = claType.GetMethod(
                "StopLoadingOnHandsChange",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
                Plugin.LogSource.LogWarning("[LoadAmmoAnim] CLA compat: could not find StopLoadingOnHandsChange method.");

            return method;
        }

        [PatchPrefix]
        public static bool Prefix()
        {
            // CLA only manages the local player. block when our local anim is active.
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null) return true;
            var session = LoadAmmoAnimState.TryGet(player);
            return session == null || !session.IsOurAnimation;
        }
    }

    // CLA calls TrySetLastEquippedWeapon when its session is wrapping up. thats our
    // cue to tear our anim down so we dont fight CLA for the weapon equip.
    public class ClaTrySetLastEquippedWeaponPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(Player), "TrySetLastEquippedWeapon",
                new[] { typeof(bool), typeof(Callback) });

        [PatchPrefix]
        public static void Prefix(Player __instance)
        {
            if (!__instance.IsYourPlayer) return;
            var session = LoadAmmoAnimState.TryGet(__instance);
            if (session == null || !session.IsOurAnimation) return;

            session.IsOurAnimation = false;
            LoadAmmoAnimDriver.StopAnimationInstantly(__instance);
        }
    }
}
