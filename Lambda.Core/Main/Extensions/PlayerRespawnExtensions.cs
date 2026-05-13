using EFT;
using UnityEngine;

namespace Lambda.Core.Main;

public static class PlayerRespawnExtensions
{
    public static Vector3 GetPosition(this Player player)
    {
        return player.Transform.Original.position;
    }
}