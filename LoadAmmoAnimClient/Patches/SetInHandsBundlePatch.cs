using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Manimal.LoadAmmoAnim.CustomEFTData;
using SPT.Reflection.Patching;

namespace Manimal.LoadAmmoAnim.Patches
{
    // routes our item through LoadAmmoBundleController when something calls
    // Player.SetInHandsUsableItem on it. vanilla only recognises specific
    // built-in types and silently no-ops for ours.
    internal sealed class SetInHandsBundlePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            typeof(Player).GetMethod(
                nameof(Player.SetInHandsUsableItem),
                BindingFlags.Public | BindingFlags.Instance);

        [PatchPrefix]
        private static bool Prefix(Player __instance, Item item, Callback<IUsableItemController> callback)
        {
            if (item is LoadAmmoBundleItem)
            {
                __instance.Proceed<LoadAmmoBundleController>(item, callback, true);
                return false;
            }
            return true;
        }
    }
}
