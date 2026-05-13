using EFT;
using UnityEngine;

public static class PlayerRespawnExtensions
{
    public static Vector3 GetPosition(this Player player)
    {
        return player.Transform.Original.position;
    }
}