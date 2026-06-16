using BepInEx.Logging;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Manimal.LoadAmmoAnim.Fika.Packets;
using Manimal.LoadAmmoAnim.Patches;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using JetBrains.Annotations;

namespace Manimal.LoadAmmoAnim.Fika;

[UsedImplicitly]
public static class FikaCompatModule
{
    private static ManualLogSource _log;
    private static IFikaNetworkManager _networkManager;

    [UsedImplicitly]
    public static void Enable()
    {
        _log = Logger.CreateLogSource("LoadAmmoAnim.Fika");
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
        _log.LogInfo("[LoadAmmoAnim.Fika] enabled.");
    }
        
    // May or may not be reliable, things get confusing when a player can be both a client and a server (I get the same issues when detecting NM_ListenServer in UE3).
    private static void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent createdEvent)
    {
        _networkManager = createdEvent.Manager;
        switch (_networkManager)
        {
            case FikaServer server:
                server.RegisterPacket<LoadAmmoBundleStartPacket, NetPeer>(OnStartPacketServer);
                server.RegisterPacket<LoadAmmoBundleStopPacket, NetPeer>(OnStopPacketServer);
                server.RegisterPacket<LoadAmmoBundleSwapMeshPacket, NetPeer>(OnSwapMeshPacketServer);
                break;
            case FikaClient client:
                client.RegisterPacket<LoadAmmoBundleStartPacket>(OnStartPacketClient);
                client.RegisterPacket<LoadAmmoBundleStopPacket>(OnStopPacketClient);
                client.RegisterPacket<LoadAmmoBundleSwapMeshPacket>(OnSwapMeshPacketClient);
                break;
        }
            
        LoadAmmoAnimEvents.AnimStarted += OnAnimStarted;
        LoadAmmoAnimEvents.AnimStopped += OnAnimStopped;
        LoadAmmoAnimEvents.MagSwapped  += OnMagSwapped;
    }

    private static FikaPlayer AsLocalFikaPlayer(Player p)
    {
        if (p is FikaPlayer { IsYourPlayer: true } fp) return fp;
        return null;
    }

    private static void OnAnimStarted(Player player, string magTpl, float speed)
    {
        var fp = AsLocalFikaPlayer(player);
        if (!fp) return;

        var pkt = new LoadAmmoBundleStartPacket
        {
            NetId = fp.NetId,
            MagTemplateId = magTpl,
            LoadOneAmmoSpeed = speed
        };
        _networkManager.SendData(ref pkt, DeliveryMethod.ReliableOrdered, true);
    }

    private static void OnAnimStopped(Player player, bool playPutAway)
    {
        var fp = AsLocalFikaPlayer(player);
        if (!fp) return;

        var pkt = new LoadAmmoBundleStopPacket
        {
            NetId = fp.NetId,
            PlayPutAway = playPutAway
        };
        _networkManager.SendData(ref pkt, DeliveryMethod.ReliableOrdered, true);
    }

    private static void OnMagSwapped(Player player, string magTpl)
    {
        var fp = AsLocalFikaPlayer(player);
        if (!fp) return;

        var pkt = new LoadAmmoBundleSwapMeshPacket
        {
            NetId = fp.NetId,
            NewMagTemplateId = magTpl
        };
        _networkManager.SendData(ref pkt, DeliveryMethod.ReliableOrdered, true);
    }
        
    private static void OnStartPacketClient(LoadAmmoBundleStartPacket pkt)
    {
        if (!_networkManager.CoopHandler.Players.TryGetValue(pkt.NetId, out var player))
            return;
        LoadAmmoAnimDriver.StartBundleAnim(player, pkt.MagTemplateId, pkt.LoadOneAmmoSpeed);
    }

    private static void OnStopPacketClient(LoadAmmoBundleStopPacket pkt)
    {
        if (!_networkManager.CoopHandler.Players.TryGetValue(pkt.NetId, out var player))
            return;
        LoadAmmoAnimDriver.StopBundleAnim(player, pkt.PlayPutAway);
    }

    private static void OnSwapMeshPacketClient(LoadAmmoBundleSwapMeshPacket pkt)
    {
        if (!_networkManager.CoopHandler.Players.TryGetValue(pkt.NetId, out var player))
            return;
        LoadAmmoAnimDriver.SwapMagMesh(player, pkt.NewMagTemplateId);
    }
        
    private static void OnStartPacketServer(LoadAmmoBundleStartPacket pkt, NetPeer sender)
    {
        var server = (FikaServer)_networkManager;
        if (!server) return;
            
        server.SendData(ref pkt, DeliveryMethod.ReliableOrdered, sender);
    }

    private static void OnStopPacketServer(LoadAmmoBundleStopPacket pkt, NetPeer sender)
    {
        var server = (FikaServer)_networkManager;
        if (!server) return;
            
        server.SendData(ref pkt, DeliveryMethod.ReliableOrdered, sender);
    }

    private static void OnSwapMeshPacketServer(LoadAmmoBundleSwapMeshPacket pkt, NetPeer sender)
    {
        var server = (FikaServer)_networkManager;
        if (!server) return;
            
        server.SendData(ref pkt, DeliveryMethod.ReliableOrdered, sender);
    }
}