using EFT.InventoryLogic;
using WTTClientCommonLib.Attributes;

namespace Manimal.LoadAmmoAnim.CustomEFTData
{
    // marker type for the virtual mag-bundle item. its existence as a class lets
    // dispatch patches do `item is LoadAmmoBundleItem` to route the controller
    // swap to LoadAmmoBundleController.
    //
    // cloned from PortableRangeFinder server-side so the engine treats it as a
    // usable info-item.
    [CustomParent(BuildInfo.BundleItemParentId, typeof(LoadAmmoBundleItem), typeof(PortableRangeFinderTemplate))]
    public sealed class LoadAmmoBundleItem : PortableRangeFinder
    {
        public LoadAmmoBundleItem(string id, PortableRangeFinderTemplate template) : base(id, template) { }
    }
}
