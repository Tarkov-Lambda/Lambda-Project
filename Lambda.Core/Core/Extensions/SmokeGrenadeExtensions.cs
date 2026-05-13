using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lambda.Core.Main;

public static class SmokeGrenadeExtensions
{
    public static async UniTaskVoid StartVelocityGateAsync(this SmokeGrenade grenade)
    {
        const float velocityThreshold = 0.15f;
        const float stableTimeRequired = 0.5f;

        float stableTimer = 0f;

        Rigidbody rb = grenade.GetComponent<Rigidbody>();

        if (rb == null)
            return;

        while (grenade != null)
        {
            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);

            float speed = rb.velocity.magnitude;

            if (speed <= velocityThreshold)
            {
                stableTimer += Time.fixedDeltaTime;

                if (stableTimer >= stableTimeRequired)
                {
                    break;
                }
            }
            else
            {
                stableTimer = 0f;
            }
        }

        TriggerSmokeBloom(grenade);
    }

    private static void TriggerSmokeBloom(SmokeGrenade grenade)
    {

    }
}