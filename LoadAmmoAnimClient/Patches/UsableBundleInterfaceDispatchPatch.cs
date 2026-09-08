using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using Manimal.LoadAmmoAnim.CustomEFTData;
using SPT.Reflection.Patching;

namespace Manimal.LoadAmmoAnim.Patches
{
    // patches NextObservedPlayer.ObservedPlayerUsableItemController.GetObservedUsableItem, the dispatcher that returns the IObservedUsableItem
    // implementation for a given item type during the controller swap chain.
    // vanilla returns specific classes for radio transmitter and rangefinder,
    // otherwise null. returning null for our item silently breaks downstream
    // code that expects a non-null instance.
    internal sealed class UsableBundleInterfaceDispatchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            typeof(EFT.NextObservedPlayer.ObservedPlayerUsableItemController).GetMethod(
                "GetObservedUsableItem",
                BindingFlags.Public | BindingFlags.Static);

        [PatchPrefix]
        private static bool Prefix(ref EFT.NextObservedPlayer.IObservedUsableItem __result, Item item)
        {
            if (item is LoadAmmoBundleItem)
            {
                __result = new LoadAmmoBundleInterfaceClass();
                return false;
            }
            return true;
        }
    }
}
