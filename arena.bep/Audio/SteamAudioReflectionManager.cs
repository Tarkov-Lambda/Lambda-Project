using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Audio.SpatialSystem;

public class SteamAudioReflectionManager : MonoBehaviour
{
    public static SteamAudioReflectionManager Instance;

    // Fast lookup for currently playing spatial sources
    public readonly HashSet<MetaSpatialAudioSource> ActiveSources = new HashSet<MetaSpatialAudioSource>();

    // Your Steam Audio reflection budget limit
    public int MaxReflections = 32;

    private float _updateTimer = 0f;
    private const float UpdateInterval = 0.1f; // 10 times a second is plenty for audio swapping

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // Ensure listener exists
        if (BetterAudio.Instance == null || BetterAudio.Instance.ListenerTransform == null)
            return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer = 0f;
        EvaluateReflectionBudget();
    }

    private void EvaluateReflectionBudget()
    {
        // 1. Clean up any destroyed or null sources
        ActiveSources.RemoveWhere(s => s == null);

        Vector3 listenerPos = BetterAudio.Instance.ListenerTransform.position;

        // 2. Score and sort active sources
        // Lower score = higher priority for receiving reflections.
        var sortedSources = ActiveSources
            .Where(s => s.gameObject.activeInHierarchy)
            .OrderBy(s => CalculatePriorityScore(s, listenerPos))
            .ToList();

        // 3. Distribute the reflection budget
        for (int i = 0; i < sortedSources.Count; i++)
        {
            bool withinBudget = i < MaxReflections;
            
            // Only push updates to the spatializer if the state actually needs to change.
            // This prevents spamming the native audio backend.
            if (sortedSources[i].EnableReverb != withinBudget)
            {
                // This toggles MetaXRAudioSource.EnableAcoustics
                sortedSources[i].EnableReverb = withinBudget; 
                
                // Pushes the updated parameter down to your Native Spatializer 
                // (e.g. source.SetSpatializerFloat(5, ...))
                sortedSources[i].UpdateParameters(); 
            }
        }
    }

    private float CalculatePriorityScore(MetaSpatialAudioSource source, Vector3 listenerPos)
    {
        // SqrMagnitude is much faster to calculate than Vector3.Distance
        float distanceSq = (source.transform.position - listenerPos).sqrMagnitude;

        // Tarkov automatically assigns AudioSource priority based on distance and the sound group.
        // Gunshots and footsteps get low numbers (high priority), ambience gets high numbers (low priority).
        // 0 is Highest, 256 is Lowest.
        float priority = source.ParentSource != null ? source.ParentSource.priority : 128f;

        // We multiply distance by priority fraction. High-priority sounds shrink the distance score,
        // virtually bringing them closer to ensure they get a reflection slot over nearby ambient wind.
        return distanceSq * Mathf.Max(0.1f, (priority / 128f));
    }
}