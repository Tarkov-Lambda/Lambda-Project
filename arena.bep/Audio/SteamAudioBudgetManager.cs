using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Audio.SpatialSystem;
using ifp.arena.shared;
using static ifp.arena.shared.PhononDSPBridge; // Your namespace for PhononDSPBridge

public class SteamAudioBudgetManager : MonoBehaviour
{
    public static SteamAudioBudgetManager Instance;
    public int MaxReflections = 8;

    public class TrackedSource
    {
        public MetaSpatialAudioSource MetaSource;
        public PhononDSPBridge PhononBridge;
        public AudioSource UnitySource;
        public Transform Transform;
        public bool HasReflections;
    }

    // Fast lookup dictionary
    public readonly Dictionary<MetaSpatialAudioSource, TrackedSource> ActiveSources = new Dictionary<MetaSpatialAudioSource, TrackedSource>();

    private float _updateTimer = 0f;
    private const float UpdateInterval = 0.1f; // Run 10 times a second

    private void Awake()
    {
        Instance = this;
    }

    public void RegisterSource(MetaSpatialAudioSource source)
    {
        if (ActiveSources.ContainsKey(source)) return;

        // Only track sources that actually have your custom bridge
        if (source.TryGetComponent(out PhononDSPBridge bridge) && source.TryGetComponent(out AudioSource aSrc))
        {
            ActiveSources[source] = new TrackedSource
            {
                MetaSource = source,
                PhononBridge = bridge,
                UnitySource = aSrc,
                Transform = source.transform,
                HasReflections = false
            };
        }
    }

    public void UnregisterSource(MetaSpatialAudioSource source)
    {
        if (ActiveSources.TryGetValue(source, out TrackedSource ts))
        {
            // Instantly free the DSP memory when the sound goes back to the object pool
            if (ts.HasReflections)
            {
                ts.PhononBridge.ToggleReflectionDSP(false);
            }
            ActiveSources.Remove(source);
        }
    }

    private void Update()
    {
        if (BetterAudio.Instance == null || BetterAudio.Instance.ListenerTransform == null)
            return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer = 0f;
        EvaluateBudget();
    }

    private void EvaluateBudget()
    {
        // Initialize the pool the first time this runs
        if (!NativeReflectionPool.IsInitialized)
        {
            NativeReflectionPool.Initialize(MaxReflections);
        }

        Vector3 listenerPos = BetterAudio.Instance.ListenerTransform.position;

        var sortedSources = ActiveSources.Values
            .OrderBy(ts => CalculatePriorityScore(ts, listenerPos))
            .ToList();

        for (int i = 0; i < sortedSources.Count; i++)
        {
            bool withinBudget = i < MaxReflections;
            TrackedSource ts = sortedSources[i];

            if (withinBudget && !ts.HasReflections)
            {
                // Sound promoted to top 32! Give it a pre-allocated DSP slot.
                var slot = NativeReflectionPool.Borrow();
                if (slot != null)
                {
                    ts.HasReflections = true;
                    ts.PhononBridge.AssignReflectionSlot(slot);
                }
            }
            else if (!withinBudget && ts.HasReflections)
            {
                // Sound demoted (or finished). Take the slot back.
                ts.HasReflections = false;
                var slot = ts.PhononBridge.RevokeReflectionSlot();
                if (slot != null) NativeReflectionPool.Return(slot);
            }
        }
    }

    private void OnDestroy()
    {
        NativeReflectionPool.Cleanup(); // Free memory when raid ends
    }

    private float CalculatePriorityScore(TrackedSource ts, Vector3 listenerPos)
    {
        // Calculate SqrMagnitude (cheaper than Vector3.Distance)
        float distanceSq = (ts.Transform.position - listenerPos).sqrMagnitude;

        // Tarkov's source.priority -> 0 is highest, 256 is lowest.
        float priority = ts.UnitySource.priority;

        // Multiply distance by the priority weight. 
        // This ensures close-proximity gunshots win the slot over close-proximity wind ambiance.
        return distanceSq * Mathf.Max(0.1f, (priority / 128f));
    }
}