using UnityEngine;

namespace PhononSpatializerProxy
{
    internal static class PhononListenerCache
    {
        private static Transform _cachedListener;
        private static int _lastListenerSearchFrame = -1;

        public static Transform GetListenerTransform()
        {
            if (_cachedListener != null && _cachedListener.gameObject.activeInHierarchy)
                return _cachedListener;

            if (Time.frameCount == _lastListenerSearchFrame)
                return _cachedListener;

            _lastListenerSearchFrame = Time.frameCount;

            var cam = Camera.main;
            if (cam != null)
            {
                _cachedListener = cam.transform;
                return _cachedListener;
            }

            var al = Object.FindObjectOfType<AudioListener>();
            if (al != null)
            {
                _cachedListener = al.transform;
                return _cachedListener;
            }

            _cachedListener = null;
            return null;
        }
    }

    internal class PhononDistanceAttenuator
    {
        private readonly AudioSource _src;
        private AnimationCurve _cachedRolloffCurve;
        private float _lastMaxDist = -1f;

        public PhononDistanceAttenuator(AudioSource source)
        {
            _src = source;
        }

        public float Calculate(float dist)
        {
            if (_src == null) return 0f;

            float minDist = _src.minDistance;
            float maxDist = Mathf.Max(_src.maxDistance, minDist + 0.001f);

            if (dist <= minDist) return 1f;
            if (dist >= maxDist) return 0f;

            if (_src.rolloffMode == AudioRolloffMode.Custom)
            {
                if (_cachedRolloffCurve == null || _lastMaxDist != maxDist)
                {
                    _cachedRolloffCurve = _src.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                    _lastMaxDist = maxDist;

                    if (_cachedRolloffCurve == null || _cachedRolloffCurve.length == 0)
                        return 1f - (dist - minDist) / (maxDist - minDist);
                }

                float norm = dist / maxDist;
                return Mathf.Clamp01(_cachedRolloffCurve.Evaluate(norm));
            }

            if (_src.rolloffMode == AudioRolloffMode.Linear)
            {
                return 1f - (dist - minDist) / (maxDist - minDist);
            }

            if (_src.rolloffMode == AudioRolloffMode.Logarithmic)
            {
                return minDist / dist;
            }

            return 1f;
        }
    }
}