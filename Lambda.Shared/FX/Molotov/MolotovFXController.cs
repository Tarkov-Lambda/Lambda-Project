using System;
using UnityEngine;

namespace Lambda.Shared.FX
{
    public class MolotovFXController : MonoBehaviour
    {
        private enum State { Disabled, Igniting, IdleOnFire, FadingOut }

        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private Light[] pointLights;

        [Space(10)]
        [SerializeField] private float maxLightIntensity = 2f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 1f;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Space(10)]
        [SerializeField] private AnimationCurve flickerCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);

        private Action<MolotovFXController> returnToPoolCallback;

        private State currentState = State.Disabled;
        private float stateTimeElapsed = 0f;

        [ContextMenu("Test Ignite")]
        private void TestIgnite()
        {
            Ignite(null);
        }

        public void Ignite(Action<MolotovFXController> returnCallback)
        {
            returnToPoolCallback = returnCallback;
            currentState = State.Igniting;

            stateTimeElapsed = 0f;

            foreach (var ps in particleSystems)
                ps.Play();
        }

        [ContextMenu("Test Stop")]
        public void StopAndFadeOut()
        {
            currentState = State.FadingOut;
            stateTimeElapsed = 0f;

            foreach (var ps in particleSystems)
                ps.Stop();
        }

        private void Update()
        {
            stateTimeElapsed += Time.deltaTime;

            switch (currentState)
            {
                case State.Disabled:
                    {
                        SetLightIntensityWithFlicker(0f);
                        break;
                    }
                case State.Igniting:
                    {
                        float t = fadeInCurve.Evaluate(Mathf.Clamp01(stateTimeElapsed / fadeInDuration));
                        SetLightIntensityWithFlicker(Mathf.Lerp(0, maxLightIntensity, t));
                        if (stateTimeElapsed > fadeInDuration)
                        {
                            stateTimeElapsed = 0f;
                            currentState = State.IdleOnFire;
                        }
                        foreach (var item in particleSystems)
                        {
                            item.transform.localScale = Vector3.one * t;
                        }
                        break;
                    }
                case State.IdleOnFire:
                    {
                        SetLightIntensityWithFlicker(maxLightIntensity);
                    }
                    break;
                case State.FadingOut:
                    {
                        float t = fadeOutCurve.Evaluate(Mathf.Clamp01(stateTimeElapsed / fadeOutDuration));
                        SetLightIntensityWithFlicker(Mathf.Lerp(maxLightIntensity, 0, t));
                        if (AreAllParticlesDead() && stateTimeElapsed > fadeOutDuration)
                        {
                            currentState = State.Disabled;
                            returnToPoolCallback?.Invoke(this);
                        }
                    }
                    break;
            }
        }

        private void SetLightIntensityWithFlicker(float intensity)
        {
            foreach (var light in pointLights)
                light.intensity = flickerCurve.Evaluate(Time.time) * intensity;
        }

        private bool AreAllParticlesDead()
        {
            foreach (var ps in particleSystems)
            {
                if (ps.IsAlive()) return false;
            }
            return true;
        }
    }
}