using Fika.Core.Main.Components;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player.Common;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.respawn
{
    public enum Gamemodes
    {
        TDM,
        SND,
        FFA
    }

    public enum Faction
    {
        CT,
        T,
        None // FFA
    }
}
