using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Manimal.LoadAmmoAnim.CustomEFTData;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Manimal.LoadAmmoAnim.Patches
{
    // event seam used by the Fika compat assembly. the main client raises these
    // for local-player session lifecycle changes, the Fika layer subscribes and
    // turns them into network packets. fire-and-forget — handlers must not throw.
    internal static class LoadAmmoAnimEvents
    {
        public static event Action<Player, string, float> AnimStarted;
        public static event Action<Player, bool> AnimStopped;
        public static event Action<Player, string> MagSwapped;

        internal static void RaiseStarted(Player p, string magTpl, float speed)
        {
            try { AnimStarted?.Invoke(p, magTpl, speed); }
            catch (Exception ex) { Plugin.LogSource?.LogError($"[LoadAmmoAnim] AnimStarted handler threw: {ex.Message}"); }
        }

        internal static void RaiseStopped(Player p, bool playPutAway)
        {
            try { AnimStopped?.Invoke(p, playPutAway); }
            catch (Exception ex) { Plugin.LogSource?.LogError($"[LoadAmmoAnim] AnimStopped handler threw: {ex.Message}"); }
        }

        internal static void RaiseMagSwapped(Player p, string magTpl)
        {
            try { MagSwapped?.Invoke(p, magTpl); }
            catch (Exception ex) { Plugin.LogSource?.LogError($"[LoadAmmoAnim] MagSwapped handler threw: {ex.Message}"); }
        }
    }

    // per-player session bag. one of these per Player that has ever started a load.
    // keeps everything that used to be on the static LoadAmmoAnimState class so
    // multi-player setups (Fika) can run independent sessions side by side.
    internal sealed class PlayerSession
    {
        // tracks how many LoadMagazineProcess sessions are alive for this player. the visible
        // anim only starts on the 0 to 1 edge.
        public int LoadingCount;

        public bool IsOurAnimation;
        public LoadAmmoBundleController ActiveController;
        public Coroutine LoopCoroutine;

        // seconds per round at the player's current mag drills level. re-read from
        // LoadMagazineProcess.Float_0 every session so skill ups take effect right away.
        public float LoadOneAmmoSpeed = 1f;

        // armed at the start of each session, consumed by LoadMagazineProcessDrawDelayPatch on
        // the first bullet only. that way we only stretch the initial delay, not every one.
        public bool DrawPhasePending;

        // template id of the mag we are loading. captured in LoadMagazineProcess.Start, so
        // ApplyMeshSelection knows which mesh to switch on.
        public string CurrentMagTemplateId;

        // the live mag item. AnimLoop checks this every frame to know when its full
        // and we should stop.
        public Magazine CurrentMag;

        // the renderer we enabled this session. cleared in tear-down so the bundle
        // returns to pool clean.
        public MeshRenderer ActiveRenderer;

        public bool IsLoading => LoadingCount > 0;
    }

    // registry of per-player sessions. patches that have a Player handle look up
    // through here. patches without one (rare) fall back to MainPlayer.
    internal static class LoadAmmoAnimState
    {
        private static readonly Dictionary<Player, PlayerSession> _sessions =
            new Dictionary<Player, PlayerSession>();

        public static PlayerSession Get(Player player)
        {
            if (player == null) return null;
            if (!_sessions.TryGetValue(player, out var s))
                _sessions[player] = s = new PlayerSession();
            return s;
        }

        public static PlayerSession TryGet(Player player)
        {
            if (player == null) return null;
            _sessions.TryGetValue(player, out var s);
            return s;
        }

        public static void Clear(Player player)
        {
            if (player == null) return;
            _sessions.Remove(player);
        }

        // global probe for patches without a player handle (eg. CLA's static methods).
        public static bool AnyIsOurAnimation()
        {
            foreach (var s in _sessions.Values)
                if (s.IsOurAnimation) return true;
            return false;
        }

        public static void OnLoadingStarted(Player player)
        {
            if (player == null) return;
            var s = Get(player);
            // only start a new anim on the 0 to 1 edge. anything else is just an extra
            // session piling on top of one we are already animating for.
            if (s.LoadingCount++ == 0 && !s.IsOurAnimation)
            {
                s.DrawPhasePending = true;
                player.StartCoroutine(LoadAmmoAnimDriver.StartNextFrame(player));
                // notify Fika compat layer so it can broadcast.
                LoadAmmoAnimEvents.RaiseStarted(player, s.CurrentMagTemplateId, s.LoadOneAmmoSpeed);
            }
        }

        public static void OnLoadingEnded(Player player)
        {
            var s = TryGet(player);
            if (s == null) return;
            if (--s.LoadingCount < 0) s.LoadingCount = 0;
        }

        // hard reset for LoadingCount, used when the stall logic decides things have gone off the rails.
        internal static void ForceResetLoading(Player player)
        {
            var s = TryGet(player);
            if (s != null) s.LoadingCount = 0;
        }
    }

    // creates the virtual bundle item, runs the controller swap, drives the per-frame
    // loop that watches mag count and triggers teardown.
    internal static class LoadAmmoAnimDriver
    {
        // matches the template id in hidden_anim_STANAGS.jsonc. factory.CreateItem
        // produces LoadAmmoBundleItem via WTTClientCommonLib's [CustomParent].
        private const string BundleItemTplId = "69d69c70ed183ba9c882f7f7";

        // length of the bundle's draw clip. LoadMagazineProcessDrawDelayPatch pads the
        // first-bullet wait by this, scaled to the actual playback speed.
        internal const float DrawClipSeconds = 0.867f;

        // ceiling on the put-away wait. clip is roughly 1s.
        private const float PutAwayMaxWaitSeconds = 1.5f;

        // CLA can dip LoadingCount to 0 for a tick between bullets, so we wait this
        // long before deciding the session is actually over.
        private const float ClaStopTimeoutSeconds = 0.3f;

        // process-wide flag, the asset cache is shared across players.
        private static bool _bundleWarmed;

        public static IEnumerator StartNextFrame(Player player)
        {
            // wait one frame so LoadMagazineProcess.Start can finish its synchronous setup.
            // proceeding inline races the inventory state.
            yield return null;
            if (player == null) yield break;

            var session = LoadAmmoAnimState.TryGet(player);
            if (session == null || !session.IsLoading) yield break;

            var bundleItem = CreateBundleItem();
            if (bundleItem == null)
            {
                Plugin.LogSource?.LogWarning("[LoadAmmoAnim] could not obtain bundle item, skipping animation.");
                yield break;
            }

            // raid-start warm-up hasnt finished yet, so retain the bundle inline here.
            // the first reload of the raid sometimes lands on this path. every reload
            // after is a no-op since _bundleWarmed sticks around.
            if (!_bundleWarmed)
            {
                var usePrefab = bundleItem.UsePrefab;
                if (usePrefab != null)
                {
                    var poolManager = Singleton<ObjectsFactory>.Instance;
                    if (poolManager?.EasyAssets != null)
                    {
                        var retainTask = EasyAssetsExtensions.RetainSeparateTask(
                            poolManager.EasyAssets, new[] { usePrefab.path });
                        while (!retainTask.IsCompleted) yield return null;

                        if (!retainTask.IsFaulted && retainTask.Result?.LoadingJob != null)
                        {
                            var loadTask = retainTask.Result.LoadingJob;
                            while (!loadTask.IsCompleted) yield return null;
                        }
                        _bundleWarmed = true;
                    }
                }
            }

            TryStart(player, bundleItem);
        }

        private static void TryStart(Player player, LoadAmmoBundleItem bundleItem)
        {
            var session = LoadAmmoAnimState.Get(player);
            session.IsOurAnimation = true;

            try { player.StopBlindFire(); } catch { }
            try { player.RemoveLeftHandItem(); } catch { }

            // hand-stitched controller swap. Player.Proceed<T> uses PoolManagerClass.CreateItem
            // as its factory, which reads the item's Prefab field. our bundle is on UsePrefab,
            // so Proceed loads PortableRangeFinder's default mesh and smethod_4 NREs trying
            // to find the WeaponPrefab component.
            //
            // Drop fires the current weapon's put-away. when it finishes, our callback
            // does destroy + create + spawn back-to-back so PWA never sees stale transforms.
            player.DropCurrentController(
                () => CreateAndSpawnBundleController(player, bundleItem),
                fastDrop: false,
                nextControllerItem: bundleItem);
        }

        // post-drop callback. CreateItemUsablePrefab is the factory delegate that reads
        // UsePrefab where our bundle path lives.
        private static void CreateAndSpawnBundleController(Player player, LoadAmmoBundleItem bundleItem)
        {
            var session = LoadAmmoAnimState.Get(player);
            try
            {
                if (player.HandsController != null)
                {
                    try { player.DestroyController(); }
                    catch (Exception ex)
                    {
                        Plugin.LogSource?.LogError(
                            $"[LoadAmmoAnim] DestroyController in spawn callback: {ex.Message}");
                    }
                }

                var controller = Player.ItemHandsController.smethod_1<LoadAmmoBundleController>(
                    player,
                    bundleItem,
                    new Player.ItemHandsController.Delegate8(
                        Singleton<ObjectsFactory>.Instance.CreateItemUsablePrefab));

                if (controller == null)
                {
                    Plugin.LogSource?.LogError("[LoadAmmoAnim] smethod_1 returned null controller.");
                    session.IsOurAnimation = false;
                    return;
                }

                Player.UsableItemController.Setup<LoadAmmoBundleController>(controller, player);

                // SpawnController calls controller.Spawn(1f, ...) which forces animator
                // speed to 1f and registers the equip-event callback. by the time that
                // callback resolves the draw clip is already done, so we set animSpeed
                // synchronously after SpawnController returns. that way it applies to
                // the entire draw, not just the post-draw loop.
                player.SpawnController(controller, () => { });

                session.ActiveController = controller;

                // tie playback to the player's current mag-drills speed.
                var firearmsAnim = controller.FirearmsAnimator;
                if (firearmsAnim != null && session.LoadOneAmmoSpeed > 0f)
                    firearmsAnim.SetAnimationSpeed(1f / session.LoadOneAmmoSpeed);

                ApplyMeshSelection(player);
                session.LoopCoroutine = player.StartCoroutine(AnimLoop(player, controller));
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogError(
                    $"[LoadAmmoAnim] CreateAndSpawnBundleController threw: {ex.GetType().Name}: {ex.Message}");
                session.IsOurAnimation = false;
            }
        }

        // builds a throwaway bundle item that only lives in memory. it never enters
        // any inventory and never gets consumed.
        private static LoadAmmoBundleItem CreateBundleItem()
        {
            try
            {
                var factory = Singleton<ItemFactory>.Instance;
                if (factory == null) return null;
                return factory.CreateItem(
                    MongoID.Generate(false).ToString(),
                    BundleItemTplId,
                    null) as LoadAmmoBundleItem;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning(
                    $"[LoadAmmoAnim] could not create bundle item: {ex.Message}");
                return null;
            }
        }

        // preloads the bundle at raid start so the first reload doesnt hitch waiting
        // for it to load.
        public static IEnumerator WarmBundleAsync()
        {
            _bundleWarmed = false;

            string bundlePath = null;
            try
            {
                var factory = Singleton<ItemFactory>.Instance;
                if (factory != null)
                {
                    var tmp = factory.CreateItem(
                        MongoID.Generate(false).ToString(),
                        BundleItemTplId, null) as LoadAmmoBundleItem;
                    bundlePath = tmp?.UsePrefab?.path;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(bundlePath)) yield break;

            var poolManager = Singleton<ObjectsFactory>.Instance;
            if (poolManager?.EasyAssets == null) yield break;

            var retainTask = EasyAssetsExtensions.RetainSeparateTask(
                poolManager.EasyAssets, new[] { bundlePath });
            while (!retainTask.IsCompleted) yield return null;

            if (!retainTask.IsFaulted && retainTask.Result?.LoadingJob != null)
            {
                var loadTask = retainTask.Result.LoadingJob;
                while (!loadTask.IsCompleted) yield return null;
            }

            _bundleWarmed = true;
        }

        // finds the target renderer and enables it. all other meshes are already
        // disabled in the bundle. controller's prefab root is the spawned bundle
        // gameobject, so GetComponentsInChildren only walks our small bundle.
        private static void ApplyMeshSelection(Player player)
        {
            var session = LoadAmmoAnimState.TryGet(player);
            if (session == null) return;

            string targetMesh = MagAnimLookup.GetMeshName(session.CurrentMagTemplateId);

            var prefabRoot = session.ActiveController?.ControllerGameObject;
            if (prefabRoot == null)
            {
                Plugin.LogSource?.LogWarning(
                    "[LoadAmmoAnim] ApplyMeshSelection: ControllerGameObject is null. is the bundle loaded?");
                return;
            }

            foreach (var mr in prefabRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.name != targetMesh) continue;
                mr.enabled = true;
                session.ActiveRenderer = mr;
                return;
            }

            Plugin.LogSource?.LogWarning(
                $"[LoadAmmoAnim] couldn't find mesh '{targetMesh}' under ControllerGameObject.");
        }

        // immediate teardown. used when bailing without animating.
        public static void StopAnimationInstantly(Player player)
        {
            var session = LoadAmmoAnimState.TryGet(player);
            if (session?.ActiveRenderer != null)
            {
                session.ActiveRenderer.enabled = false;
                session.ActiveRenderer = null;
            }
            if (player?.HandsController is LoadAmmoBundleController)
            {
                try { player.DestroyController(); }
                catch (Exception ex)
                {
                    Plugin.LogSource?.LogError(
                        $"[LoadAmmoAnim] DestroyController failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        // terminal teardown. play put-away with the mesh visible, then once the mag
        // is off-screen disable it, destroy the controller, equip the last weapon.
        // DestroyController + TrySetLastEquippedWeapon back-to-back hits Process.Execute's
        // null-hands fast-path so the swap is synchronous and PWA rebinds cleanly.
        internal static IEnumerator PlayPutawayThenRestore(
            Player player,
            LoadAmmoBundleController controller)
        {
            controller?.PlayPutAway();

            float deadline = Time.unscaledTime + PutAwayMaxWaitSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (controller == null) break;
                if (controller.IsPutAwayNearlyDone()) break;
                yield return null;
            }

            var session = LoadAmmoAnimState.TryGet(player);
            if (session?.ActiveRenderer != null)
            {
                session.ActiveRenderer.enabled = false;
                session.ActiveRenderer = null;
            }

            if (player == null) yield break;

            if (player.HandsController is LoadAmmoBundleController)
            {
                try { player.DestroyController(); }
                catch (Exception ex)
                {
                    Plugin.LogSource?.LogError(
                        $"[LoadAmmoAnim] DestroyController failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (!(player.HandsController is Player.FirearmController))
                player.TrySetLastEquippedWeapon(true, null);
        }

        // entry point for the Fika compat layer to start the anim on a remote
        // (observed) player when a Start packet arrives. doesnt fire AnimStarted —
        // only the local LoadMagazineProcess path does, to avoid re-broadcast loops.
        //
        // sets LoadingCount = 1 manually so AnimLoop's IsLoading check passes
        // until StopBundleAnim flips it back. observed players have no LoadMagazineProcess
        // to drive the count naturally.
        internal static void StartBundleAnim(Player player, string magTemplateId, float loadOneAmmoSpeed)
        {
            if (player == null) return;
            var session = LoadAmmoAnimState.Get(player);
            // already running for this player (eg. duplicate packet) — ignore.
            if (session.IsOurAnimation) return;

            session.LoadOneAmmoSpeed = loadOneAmmoSpeed > 0f ? loadOneAmmoSpeed : 1f;
            session.CurrentMagTemplateId = magTemplateId;
            session.CurrentMag = null;          // observed: we dont have the source's mag item
            session.LoadingCount = 1;           // synthetic, kept up by Stop packet
            session.DrawPhasePending = false;   // LoadMagazineProcessDrawDelayPatch is local-only

            player.StartCoroutine(StartNextFrame(player));
        }

        // entry point for the Fika compat layer to end an observed player's anim
        // when a Stop packet arrives. playPutAway=true gives the put-away clip,
        // false destroys instantly. doesnt fire AnimStopped to avoid re-broadcast.
        internal static void StopBundleAnim(Player player, bool playPutAway)
        {
            var session = LoadAmmoAnimState.TryGet(player);
            if (session == null) return;

            var controller = session.ActiveController;
            session.LoadingCount = 0;
            session.IsOurAnimation = false;

            if (playPutAway && controller != null && player != null)
                player.StartCoroutine(PlayPutawayThenRestore(player, controller));
            else
                StopAnimationInstantly(player);
        }

        // entry point for the Fika compat layer to swap the visible mag mesh on
        // an observed player when a SwapMesh packet arrives. mirrors the inline
        // chained-mag block inside AnimLoop. doesnt fire MagSwapped.
        internal static void SwapMagMesh(Player player, string newMagTemplateId)
        {
            var session = LoadAmmoAnimState.TryGet(player);
            if (session == null || !session.IsOurAnimation) return;
            session.CurrentMagTemplateId = newMagTemplateId;
            var controller = session.ActiveController;
            if (controller != null && player != null)
                player.StartCoroutine(SwapMagMeshCoroutine(player, controller));
        }

        private static IEnumerator SwapMagMeshCoroutine(Player player, LoadAmmoBundleController controller)
        {
            controller.PlayPutAway();

            float deadline = Time.unscaledTime + PutAwayMaxWaitSeconds;
            while (Time.unscaledTime < deadline)
            {
                if (controller == null) break;
                if (controller.IsPutAwayNearlyDone()) break;
                yield return null;
            }

            var session = LoadAmmoAnimState.TryGet(player);
            if (session?.ActiveRenderer != null)
            {
                session.ActiveRenderer.enabled = false;
                session.ActiveRenderer = null;
            }
            ApplyMeshSelection(player);

            controller.PlayDraw();
        }

        private static IEnumerator AnimLoop(Player player, LoadAmmoBundleController controller)
        {
            var session = LoadAmmoAnimState.Get(player);
            bool claInstalled = ContinuousLoadAmmoCompat.IsInstalled;
            float idleSince = -1f;

            // snapshot the mag this session was started for. once the next mag's
            // LoadMagazineProcess.Start fires, CurrentMag gets overwritten, so we cant trust
            // it inside the loop body.
            Magazine sessionMag = session.CurrentMag;

            while (session.IsOurAnimation)
            {
                yield return null;

                if (!session.IsOurAnimation || controller == null)
                    break;

                if (!session.IsLoading)
                {
                    if (!claInstalled)
                    {
                        // always run put-away + destroy + re-equip, regardless of
                        // inventory state. instant-teardown leaves PWA stale.
                        session.IsOurAnimation = false;
                        LoadAmmoAnimEvents.RaiseStopped(player, true);
                        if (player != null)
                            player.StartCoroutine(PlayPutawayThenRestore(player, controller));
                        break;
                    }

                    if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - idleSince >= ClaStopTimeoutSeconds)
                    {
                        session.IsOurAnimation = false;
                        LoadAmmoAnimEvents.RaiseStopped(player, true);
                        if (player != null)
                            player.StartCoroutine(PlayPutawayThenRestore(player, controller));
                        break;
                    }

                    continue;
                }

                idleSince = -1f;

                if (sessionMag != null && sessionMag.Count >= sessionMag.MaxCount)
                {
                    bool nextMagAlreadyLoading = session.CurrentMag != null
                                                 && session.CurrentMag != sessionMag;

                    // chained mag. play put-away, swap mesh while off-screen, play
                    // draw, all on the same controller. keeps the controller alive
                    // across the swap so the next mag flows in seamlessly.
                    if (nextMagAlreadyLoading)
                    {
                        controller.PlayPutAway();

                        float deadline = Time.unscaledTime + PutAwayMaxWaitSeconds;
                        while (Time.unscaledTime < deadline)
                        {
                            if (controller == null) break;
                            if (controller.IsPutAwayNearlyDone()) break;
                            yield return null;
                        }

                        if (session.ActiveRenderer != null)
                        {
                            session.ActiveRenderer.enabled = false;
                            session.ActiveRenderer = null;
                        }
                        ApplyMeshSelection(player);
                        sessionMag = session.CurrentMag;
                        LoadAmmoAnimEvents.RaiseMagSwapped(player, session.CurrentMagTemplateId);

                        controller.PlayDraw();
                        continue;
                    }

                    // terminal. put-away + destroy + re-equip.
                    session.IsOurAnimation = false;
                    LoadAmmoAnimState.ForceResetLoading(player);
                    LoadAmmoAnimEvents.RaiseStopped(player, true);
                    if (player != null)
                        player.StartCoroutine(PlayPutawayThenRestore(player, controller));

                    break;
                }
            }

            session.LoopCoroutine = null;
            session.ActiveController = null;
        }
    }

    // catches LoadMagazineProcess.Start, so we can grab the mag's per-bullet speed and template
    // id before the loading session actually starts.
    public class LoadAmmoAnimDetectPatch : ModulePatch
    {
        private static FieldInfo _float0Field;
        private static FieldInfo _magazineField;

        protected override MethodBase GetTargetMethod()
        {
            var loadMagazineProcess = AccessTools.Inner(typeof(Player.PlayerInventoryController), "LoadMagazineProcess");
            return AccessTools.Method(loadMagazineProcess, "Start");
        }

        [PatchPrefix]
        private static void Prefix(object __instance)
        {
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null) return;
            var session = LoadAmmoAnimState.Get(player);

            var type = __instance.GetType();

            // Float_0 is loadOneAmmoSpeed (seconds per round at the player's current
            // skill level).
            if (_float0Field == null)
                _float0Field = type.GetField("Float_0",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_float0Field != null)
            {
                float speed = (float)_float0Field.GetValue(__instance);
                if (speed > 0f) session.LoadOneAmmoSpeed = speed;
            }

            // bsg's obfuscator names this field after its type, so the field name
            // really is "MagazineItemClass".
            if (_magazineField == null)
                _magazineField = type.GetField("Magazine",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            session.CurrentMagTemplateId = null;
            session.CurrentMag = null;
            if (_magazineField != null)
            {
                var mag = _magazineField.GetValue(__instance) as Magazine;
                if (mag != null)
                {
                    session.CurrentMagTemplateId = mag.TemplateId;
                    session.CurrentMag = mag;
                }
            }

            LoadAmmoAnimState.OnLoadingStarted(player);
        }

        [PatchPostfix]
        private static void Postfix(Task<IResult> __result)
        {
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null) return;
            // LoadMagazineProcess.Start returns a Task that resolves when the session ends.
            // hook the continuation so LoadingCount decrements no matter how it ends.
            __result?.ContinueWith(_ => LoadAmmoAnimState.OnLoadingEnded(player));
        }
    }

    // method_5 on LoadMagazineProcess is the per-bullet wait. on the first bullet of a session
    // we extend it by the draw clip duration so the bundle's draw anim has time to
    // finish before a round actually loads. every bullet after gets the normal wait.
    public class LoadMagazineProcessDrawDelayPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var loadMagazineProcess = AccessTools.Inner(typeof(Player.PlayerInventoryController), "LoadMagazineProcess");
            return AccessTools.Method(loadMagazineProcess, "method_5");
        }

        [PatchPrefix]
        public static bool Prefix(object __instance, ref Task __result)
        {
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player == null) return true;
            var session = LoadAmmoAnimState.TryGet(player);
            if (session == null || !session.DrawPhasePending) return true;
            session.DrawPhasePending = false;

            // animator runs at 1 / LoadOneAmmoSpeed, so the actual draw-clip duration
            // is DrawClipSeconds * LoadOneAmmoSpeed. without this, fast loads finish
            // the draw and idle, slow loads cut it short.
            int normalMs = Mathf.CeilToInt(session.LoadOneAmmoSpeed * 1000f);
            int drawMs = Mathf.RoundToInt(
                LoadAmmoAnimDriver.DrawClipSeconds * session.LoadOneAmmoSpeed * 1000f);

            __result = Task.Delay(drawMs + normalMs);
            return false;
        }
    }

    // preload the bundle as soon as the raid starts so the first reload in-raid
    // doesnt hitch waiting for it to load.
    public class RaidStartBundleWarmPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            typeof(GameWorld).GetMethod("OnGameStarted");

        [PatchPostfix]
        public static void Postfix()
        {
            var player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player != null)
                player.StartCoroutine(LoadAmmoAnimDriver.WarmBundleAsync());
        }
    }

    // every PWA method that runs from LateUpdate eventually derefs WeaponRootAnim
    // with no null check (ApplyPosition, ApplyComplexRotation, AvoidObstacles,
    // LerpCamera, FOV coroutines). guarding ProcessEffectors at the top of the
    // call chain catches almost all of them. costs one frame of skipped weapon-
    // position processing during a controller swap.
    public class ProcessEffectorsNullGuardPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(EFT.Animations.ProceduralWeaponAnimation), "ProcessEffectors");

        [PatchPrefix]
        public static bool Prefix(EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            if (__instance?.HandsContainer?.WeaponRootAnim == null) return false;
            return true;
        }
    }

    // Player.VisualPass directly reads WeaponRootAnim.localPosition / localRotation
    // (Player.cs lines 1447-1448) AFTER calling ProcessEffectors. patching VisualPass
    // bails before either dereference.
    public class VisualPassNullGuardPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            AccessTools.Method(typeof(Player), "VisualPass");

        [PatchPrefix]
        public static bool Prefix(Player __instance)
        {
            if (__instance?.ProceduralWeaponAnimation?.HandsContainer?.WeaponRootAnim == null)
                return false;
            return true;
        }
    }
}
