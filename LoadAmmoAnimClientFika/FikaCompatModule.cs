using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.BaseClasses;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using Manimal.LoadAmmoAnim;
using Manimal.LoadAmmoAnim.Fika.Packets;
using Manimal.LoadAmmoAnim.Patches;
using System;
using System.Threading.Tasks;

namespace Manimal.LoadAmmoAnim.Fika
{
    // sideloaded entry point. main plugin's Awake calls Enable() via reflection
    // when Fika is detected. registers packet handlers, subscribes to driver
    // events, and broadcasts/receives anim sessions across the Fika network.
    public static class FikaCompatModule
    {
        // separate logger so Fika-related lines are easy to grep in BepInEx.log.
        private static ManualLogSource _log;

        public static void Enable()
        {
            _log = BepInEx.Logging.Logger.CreateLogSource("LoadAmmoAnim.Fika");

            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);

            if (Plugin.IsHeadless)
            {
                _log.LogInfo("[LoadAmmoAnim.Fika] enabled (headless, receive only).");
                return;
            }

            try
            {
                new Harmony(BuildInfo.ModGuid + ".fika").Patch(
                    AccessTools.Method(AccessTools.Inner(typeof(BaseInventoryController), "CustomAmmoLoader"), "Start"),
                    prefix: new HarmonyMethod(typeof(FikaCompatModule), nameof(FastLoadPrefix)),
                    postfix: new HarmonyMethod(typeof(FikaCompatModule), nameof(FastLoadPostfix)));
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[LoadAmmoAnim.Fika] fast load will not animate: {ex.Message}");
            }

            // subscribe to driver events for the local-player session lifecycle.
            // every event filters on IsYourPlayer so we only ever broadcast for the
            // local player. observed-player session changes are driven by inbound
            // packets, never re-broadcast.
            LoadAmmoAnimEvents.AnimStarted += OnAnimStarted;
            LoadAmmoAnimEvents.AnimStopped += OnAnimStopped;
            LoadAmmoAnimEvents.MagSwapped  += OnMagSwapped;

            _log.LogInfo("[LoadAmmoAnim.Fika] enabled.");
        }

        private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent created)
        {
            var net = created.Manager;
            net.RegisterPacket<LoadAmmoBundleStartPacket>(OnStartPacket);
            net.RegisterPacket<LoadAmmoBundleStopPacket>(OnStopPacket);
            net.RegisterPacket<LoadAmmoBundleSwapMeshPacket>(OnSwapMeshPacket);
        }

        // Fika's fast load runs a private loader instead of LoadMagazineProcess. it loads several
        // rounds per tick, and _baseLoadSpeed is the seconds per tick.
        private static void FastLoadPrefix(
            InventoryController ____inventoryController, Magazine ____magazine,
            Ammo ____sourceAmmo, float ____baseLoadSpeed) =>
            LoadAmmoAnimState.BeginLoad(____inventoryController, ____magazine, ____sourceAmmo, ____baseLoadSpeed);

        private static void FastLoadPostfix(InventoryController ____inventoryController, Task<IResult> __result) =>
            LoadAmmoAnimState.TrackLoadEnd(____inventoryController, __result);

        // ---- send side ----

        private static void Broadcast<T>(ref T packet) where T : INetSerializable
        {
            if (!Singleton<IFikaNetworkManager>.Instantiated)
            {
                _log?.LogWarning($"[LoadAmmoAnim.Fika] cant send {typeof(T).Name}: no network manager.");
                return;
            }
            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, broadcast: true);
        }

        private static FikaPlayer AsLocalFikaPlayer(Player p)
        {
            // only the local player should ever broadcast. observed players have
            // IsYourPlayer=false and exist on every machine, so re-broadcasting
            // their state would feedback-loop.
            if (p is FikaPlayer fp && fp.IsYourPlayer) return fp;
            return null;
        }

        private static void OnAnimStarted(Player player, string magTpl, float speed)
        {
            var fp = AsLocalFikaPlayer(player);
            if (fp == null) return;

            var pkt = new LoadAmmoBundleStartPacket
            {
                NetId = fp.NetId,
                MagTemplateId = magTpl ?? string.Empty,
                LoadOneAmmoSpeed = speed
            };
            Broadcast(ref pkt);
        }

        private static void OnAnimStopped(Player player, bool playPutAway)
        {
            var fp = AsLocalFikaPlayer(player);
            if (fp == null) return;

            var pkt = new LoadAmmoBundleStopPacket
            {
                NetId = fp.NetId,
                PlayPutAway = playPutAway
            };
            Broadcast(ref pkt);
        }

        private static void OnMagSwapped(Player player, string magTpl)
        {
            var fp = AsLocalFikaPlayer(player);
            if (fp == null) return;

            var pkt = new LoadAmmoBundleSwapMeshPacket
            {
                NetId = fp.NetId,
                NewMagTemplateId = magTpl ?? string.Empty
            };
            Broadcast(ref pkt);
        }

        // ---- receive side ----

        // resolves the Player whose FikaPlayer.NetId matches. ignores the local
        // player so an echoed broadcast doesnt double-trigger our own anim.
        private static Player ResolvePlayerByNetId(int netId)
        {
            var gw = Singleton<GameWorld>.Instance;
            if (gw == null) return null;

            // GameWorld.AllAlivePlayersList is a List<Player> with the live combat
            // roster — includes both local and observed.
            var list = gw.AllAlivePlayersList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p is FikaPlayer fp && fp.NetId == netId && !fp.IsYourPlayer)
                        return p;
                }
            }

            // fallback: AllPlayersEverExisted is IEnumerable<Player> covering
            // everyone whos joined this raid, alive or not.
            var all = gw.AllPlayersEverExisted;
            if (all != null)
            {
                foreach (var p in all)
                {
                    if (p is FikaPlayer fp && fp.NetId == netId && !fp.IsYourPlayer)
                        return p;
                }
            }

            return null;
        }

        private static void OnStartPacket(LoadAmmoBundleStartPacket pkt)
        {
            if (Plugin.IsHeadless) return;
            try
            {
                var player = ResolvePlayerByNetId(pkt.NetId);
                if (player == null)
                {
                    _log?.LogDebug($"[LoadAmmoAnim.Fika] Start: no player for NetId {pkt.NetId} (likely not joined yet).");
                    return;
                }
                LoadAmmoAnimDriver.StartBundleAnim(player, pkt.MagTemplateId, pkt.LoadOneAmmoSpeed);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[LoadAmmoAnim.Fika] OnStartPacket threw: {ex}");
            }
        }

        private static void OnStopPacket(LoadAmmoBundleStopPacket pkt)
        {
            if (Plugin.IsHeadless) return;
            try
            {
                var player = ResolvePlayerByNetId(pkt.NetId);
                if (player == null)
                {
                    _log?.LogDebug($"[LoadAmmoAnim.Fika] Stop: no player for NetId {pkt.NetId}.");
                    return;
                }
                LoadAmmoAnimDriver.StopBundleAnim(player, pkt.PlayPutAway);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[LoadAmmoAnim.Fika] OnStopPacket threw: {ex}");
            }
        }

        private static void OnSwapMeshPacket(LoadAmmoBundleSwapMeshPacket pkt)
        {
            if (Plugin.IsHeadless) return;
            try
            {
                var player = ResolvePlayerByNetId(pkt.NetId);
                if (player == null)
                {
                    _log?.LogDebug($"[LoadAmmoAnim.Fika] SwapMesh: no player for NetId {pkt.NetId}.");
                    return;
                }
                LoadAmmoAnimDriver.SwapMagMesh(player, pkt.NewMagTemplateId);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[LoadAmmoAnim.Fika] OnSwapMeshPacket threw: {ex}");
            }
        }
    }
}
