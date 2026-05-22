using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EFT;
using Fika.Core.Main.Players;
using MemoryPack;
using PacketWarden;
using UnityEngine;

namespace Lambda.Core.Networking;

[MemoryPackable]
public partial struct ShutdownAnnouncementPacket : IPacket
{
    public string msg;
}

public class ShutdownAnnouncementPacketWarden : LambdaPacketWarden<ShutdownAnnouncementPacket>
{
    protected override PacketAuthority Authority => PacketAuthority.Admin;

    public void Send(string msg)
    {
        var packet = new ShutdownAnnouncementPacket
        {
            msg = msg
        };
        DispatchPacket(ref packet);
    }

    protected override void Apply(ShutdownAnnouncementPacket packet, int peerId)
    {
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.Delay(10000);
            Application.Quit();
        });
    }
}