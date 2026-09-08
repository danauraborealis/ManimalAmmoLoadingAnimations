using System.Reflection;
using EFT;
using HarmonyLib;
using Manimal.LoadAmmoAnim.CustomEFTData;
using SPT.Reflection.Patching;

namespace Manimal.LoadAmmoAnim.Patches
{
    // patches EFT.NextObservedPlayer.ObservedPlayerHandsController.GetWeaponAnimationType, the function that decides which
    // PlayerAnimator.EWeaponAnimationType the held item uses. vanilla only knows
    // pistols, revolvers, knives, etc, and falls through to a default that
    // leaves the player animator in a bad state for us.
    //
    // we force Pistol. one-handed object held in front of the camera is the
    // closest fit for a magazine.
    internal sealed class HandsControllerAnimationTypeBundlePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            typeof(EFT.NextObservedPlayer.ObservedPlayerHandsController).GetMethod(
                "GetWeaponAnimationType",
                BindingFlags.Public | BindingFlags.Instance);

        [PatchPrefix]
        private static bool Prefix(
            ref PlayerAnimator.EWeaponAnimationType __result,
            EFT.NextObservedPlayer.ObservedPlayerHandsController __instance)
        {
            if (__instance.ItemInHands is LoadAmmoBundleItem)
            {
                __result = PlayerAnimator.EWeaponAnimationType.Pistol;
                return false;
            }
            return true;
        }
    }
}
