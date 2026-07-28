using System;
using System.Collections.Generic;
using SteamAudio;
using UnityEngine;

namespace PhononSpatializerProxy
{
    // this lifecycle sucks
    public class ProxyDSPBridgeUpdateManager : MonoBehaviour, IDisposable
    {
        public static ProxyDSPBridgeUpdateManager Instance { get; private set; }
        public readonly List<PhononDSPBridge> ActiveBridges = new();

        public static void Initialize()
        {
            if (Instance != null) return;
            var go = new GameObject("PhononUpdateManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ProxyDSPBridgeUpdateManager>();

            PhononDSPBridge.OnBridgeEnabled += Instance.OnBridgeEnabled;
            PhononDSPBridge.OnBridgeDisabled += Instance.OnBridgeDisabled;
        }

        // TODO: Refactor
        // 1. SteamAudioSourceController.GetOrAdd is CWT lookup
        // 2. goofy responsibility between ProxyDSPBridgeUpdateManager and SteamAudioSourceController
        public void OnBridgeEnabled(PhononDSPBridge bridge)
        {
            ActiveBridges.Add(bridge);
            var data = SteamAudioSourceController.GetOrAdd(bridge.AudioSource);
            SteamAudioSourceController.RerouteSpatialValues(data, bridge.AudioSource);
        }

        public void OnBridgeDisabled(PhononDSPBridge bridge)
        {
            ActiveBridges.Remove(bridge);
            var data = SteamAudioSourceController.GetOrAdd(bridge.AudioSource);
            SteamAudioSourceController.RestoreSpatialValues(data, bridge.AudioSource);
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
            SteamAudioListener listener = SteamAudioManager.GetSteamAudioListener();
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