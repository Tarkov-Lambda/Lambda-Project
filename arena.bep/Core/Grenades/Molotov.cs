using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.Interactive;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.networking;
using ifp.arena.shared.FX;
using UnityEngine;

namespace ifp.arena.bep.Core;

public static class Molotov
{
    public static float initialDelay = 1f;
    public static float duration = 7f;
    public static float startRadius = 1f;
    public static float endRadius = 6f;

    public async static UniTask Spawn(CustomGrenadeExplosionPacket packet)
    {
        GameObject molotov = new GameObject("Molotov");
        molotov.transform.position = packet.explosionPos;

        SphereCollider sCollider = molotov.AddComponent<SphereCollider>();

        float elapsed = 0f;

        FlameDamageTrigger flameDamageTrigger = molotov.AddComponent<FlameDamageTrigger>();
        MolotovFXController molotovFX = H.FXHandler.SpawnMolotov(packet.explosionPos, startRadius, endRadius, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            sCollider.radius = Mathf.Lerp(startRadius, endRadius, t);

            await UniTask.Yield();
        }

        sCollider.radius = endRadius;

        await UniTask.WaitForSeconds(7);

        molotovFX.StopAndFadeOut();
        GameObject.DestroyImmediate(flameDamageTrigger);
        GameObject.DestroyImmediate(molotov);

        return;
    }
}