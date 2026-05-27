using System;
using System.Collections.Generic;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy
{
    public class PhononUpdateManager : MonoBehaviour, IDisposable
    {
        public static PhononUpdateManager Instance { get; private set; }
        public readonly List<PhononDSPBridge> ActiveBridges = new();

        public static void Initialize()
        {
            if (Instance != null) return;
            var go = new GameObject("PhononUpdateManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PhononUpdateManager>();
        }

#if UNITY_EDITOR
        public void Awake()
        {
            Instance = this;
        }
#endif

        public void Dispose()
        {
            ActiveBridges.Clear();
        }

        private void Update()
        {
            SteamAudioListener  listener = SteamAudioManager.GetSteamAudioListener();
            if (listener == null) return;

            listener.transform.GetPositionAndRotation(out UnityEngine.Vector3 listenerPos, out Quaternion listenerRot);

            for (int i = ActiveBridges.Count - 1; i >= 0; i--)
            {
                var bridge = ActiveBridges[i];
                
                if (bridge == null || !bridge.isActiveAndEnabled)
                {
                    ActiveBridges.RemoveAt(i);
                    continue;
                }

                bridge.MainThreadTick(listenerPos, listenerRot);
            }
        }
    }
}