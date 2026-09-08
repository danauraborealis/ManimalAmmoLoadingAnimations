using UnityEngine;

namespace Manimal.LoadAmmoAnim.CustomEFTData
{
    // stub IObservedUsableItem the dispatch patch hands back when GClass2970.smethod_0
    // is called for our item. without it, smethod_0 returns null and downstream
    // controller-swap code that expects a non-null instance silently breaks.
    public class LoadAmmoBundleInterfaceClass : EFT.NextObservedPlayer.IObservedUsableItem
    {
        public void Initialize(GameObject gameObject) { }
        public void UpdateData(EFT.NextObservedPlayer.ObservedUsableItemUpdatedData observedUsableItemUpdatedData) { }
        public void Disable() { }
    }
}
