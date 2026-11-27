using System;
using EFT;
using UnityEngine;

namespace Manimal.LoadAmmoAnim.CustomEFTData
{
    // hands controller for the mag-loading bundle. binds the animator inside
    // InitializeController when the engine attaches us to the spawned WeaponPrefab.
    public class LoadAmmoBundleController : Player.UsableItemController
    {
        // layer 1 is the firearms-anim convention used everywhere FirearmsAnimator's
        // Play call sites live. our bundle borrows the IFAK animator graph.
        internal const int HandsLayer = 1;

        // state names in the bundle's animator graph.
        private const string PutAwayStateName = "USE TO OUT S";
        private const string DrawStateName    = "OUT TO USE S";

        public Animator BundleAnimator { get; private set; }

        // which family's copy of the states to play. set before the draw, and again when a chained
        // magazine belongs to another family.
        internal MagAnimFamily Family = MagAnimFamily.Stanag556;

        private string PutAwayState => PutAwayStateName + Family.StateSuffix;
        private string DrawState => DrawStateName + Family.StateSuffix;

        public override void InitializeController(Player player, WeaponPrefab weaponPrefab)
        {
            try
            {
                base.InitializeController(player, weaponPrefab);

                if (weaponPrefab == null) return;
                BundleAnimator = weaponPrefab.GetComponentInChildren<Animator>();
                if (BundleAnimator == null) return;

                var session = Patches.LoadAmmoAnimState.TryGet(player);
                Family = MagAnimLookup.GetFamily(session?.CurrentMagTemplateId);

                // pool-reuse reset. between sessions the bundle gameobject is just
                // deactivated, not destroyed, so the animator wakes up in whatever
                // state it was in last session. force-jump to the draw state and
                // SetActive(true) so the engine's use-loop machinery takes over.
                BundleAnimator.Play(DrawState, HandsLayer, 0f);
                BundleAnimator.Update(0f);
                BundleAnimator.SetBool("Active", true);
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError(
                    $"[LoadAmmoAnim] LoadAmmoBundleController.InitializeController threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // play the put-away clip. SetActive(false) handles the engine side; the
        // explicit Play covers the case where the animator graph has no transition
        // out of the put-away state once entered.
        public void PlayPutAway()
        {
            if (BundleAnimator == null) return;
            try
            {
                BundleAnimator.SetBool("Active", false);
                BundleAnimator.Play(PutAwayState, HandsLayer, 0f);
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError(
                    $"[LoadAmmoAnim] PlayPutAway: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // play the draw clip. used by the chained-mag transition after the mesh
        // has been swapped off-screen.
        public void PlayDraw()
        {
            if (BundleAnimator == null) return;
            try
            {
                BundleAnimator.Play(DrawState, HandsLayer, 0f);
                BundleAnimator.SetBool("Active", true);
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError(
                    $"[LoadAmmoAnim] PlayDraw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // true once the put-away clip has played past 95% normalized time. AnimLoop
        // polls this so it knows when the mag is off-screen and its safe to swap.
        public bool IsPutAwayNearlyDone()
        {
            if (BundleAnimator == null) return true;
            try
            {
                var info = BundleAnimator.GetCurrentAnimatorStateInfo(HandsLayer);
                return info.IsName(PutAwayState) && info.normalizedTime >= 0.95f;
            }
            catch { return true; }
        }

        private static readonly string[] BaseStates =
            { "OUT TO USE S", "BREAK -1 S", "USE S 0", "USE S 1", "USE TO OUT S" };

        // the graph's own way in (idle, then Active) leads to the first family's states. if the animator
        // restarts and takes it while another family is loading, step across to that family's copy.
        public void KeepInFamily()
        {
            if (BundleAnimator == null || Family.StateSuffix.Length == 0) return;
            try
            {
                var info = BundleAnimator.GetCurrentAnimatorStateInfo(HandsLayer);
                for (int i = 0; i < BaseStates.Length; i++)
                {
                    if (!info.IsName(BaseStates[i])) continue;
                    BundleAnimator.Play(BaseStates[i] + Family.StateSuffix, HandsLayer, info.normalizedTime % 1f);
                    return;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError($"[LoadAmmoAnim] KeepInFamily: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
