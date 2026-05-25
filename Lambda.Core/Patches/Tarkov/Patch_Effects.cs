using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using Systems.Effects;

namespace Lambda.Core.Patches.Tarkov;

public class Patch_Effects_GetEmissionEffect : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Effects), nameof(Effects.GetEmissionEffect));

    [PatchPostfix]
    static void Postfix(string key, GrenadeEmission __result)
    {
        if (__result != null)
        {
            ParticleSystem[] particleSystems = __result.GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in particleSystems)
            {
                if (ps.name != "Effect Smoke Volume")
                {
                    continue;
                }

                // ps.Stop();

                var main = ps.main;
                var shape = ps.shape;
                var emission = ps.emission;
                var noise = ps.noise;
                var vel = ps.velocityOverLifetime;
                var force = ps.forceOverLifetime;
                var sizeOverLife = ps.sizeOverLifetime;
                var colorOverLife = ps.colorOverLifetime;

                main.startDelay = 4;
                main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 18f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 5000;
                main.loop = false;

                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(6f, 3.5f, 6f); // spread area

                emission.rateOverTime = 0f;
                emission.SetBursts(
                [
                    new ParticleSystem.Burst(0f, 200),
                    new ParticleSystem.Burst(0.5f, 150)
                ]);

                vel.enabled = false;
                vel.space = ParticleSystemSimulationSpace.Local;

                vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
                vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

                noise.enabled = false;
                noise.strength = 0.4f;
                noise.frequency = 0.25f;
                noise.scrollSpeed = 0.2f;
                noise.octaveCount = 2;

                force.enabled = false;
                // force.space = ParticleSystemSimulationSpace.Local;
                // force.x = 0.1f;
                // force.y = 0.2f;
                // force.z = 0.1f;

                sizeOverLife.enabled = true;
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.3f),
                    new Keyframe(0.1f, 1.0f),  // quick puff
                    new Keyframe(0.7f, 1.2f),  // slight expansion
                    new Keyframe(1f, 1.0f)
                ));

                colorOverLife.enabled = true;
                colorOverLife.color = new ParticleSystem.MinMaxGradient(
                    new Gradient()
                    {
                        colorKeys = new[]
                        {
                            new GradientColorKey(Color.grey, 0f),
                            new GradientColorKey(Color.grey, 1f)
                        },
                        alphaKeys = new[]
                        {
                            new GradientAlphaKey(0f, 0f),
                            new GradientAlphaKey(0.6f, 0.05f),
                            new GradientAlphaKey(0.5f, 0.8f),
                            new GradientAlphaKey(0f, 1f)
                        }
                    }
                );
            }
        }
    }
}

