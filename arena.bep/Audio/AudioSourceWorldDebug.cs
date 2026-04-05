using System.Collections.Generic;
using UnityEngine;

public class AudioSourceWorldDebug : MonoBehaviourSingleton<AudioSourceWorldDebug>
{
    public List<AudioSource> audioSources = new List<AudioSource>();
    private Camera cam;

    void FixedUpdate()
    {
        if (CameraClass.Instance != null) cam = CameraClass.Instance.Camera;
    }

    void OnGUI()
    {
        if (cam == null) return;
        if (audioSources == null) return;

        // Group sources by screen position (rounded to reduce jitter)
        Dictionary<Vector2, List<AudioSource>> grouped = new Dictionary<Vector2, List<AudioSource>>();

        foreach (AudioSource audio in audioSources)
        {
            if (audio == null) continue;

            UnityEngine.Vector3 screenPos = cam.WorldToScreenPoint(audio.transform.position);

            if (screenPos.z < 0)
                continue;

            screenPos.y = Screen.height - screenPos.y;

            // Round to group nearby objects together
            Vector2 key = new Vector2(Mathf.Round(screenPos.x / 5f) * 5f,
                                      Mathf.Round(screenPos.y / 5f) * 5f);

            if (!grouped.ContainsKey(key))
                grouped[key] = new List<AudioSource>();

            grouped[key].Add(audio);
        }

        foreach (var group in grouped)
        {
            Vector2 basePos = group.Key;
            List<AudioSource> list = group.Value;

            for (int i = 0; i < list.Count; i++)
            {
                AudioSource audio = list[i];

                // Calculate Distance and Volume
                float distance = Vector3.Distance(cam.transform.position, audio.transform.position);
                float baseVol = audio.volume;

                string status = audio.enabled ? "On" : "Off";
                string dspStat = "";

                // Use TryGetValue (slightly more performant than ContainsKey + Indexer)
                if (SteamSourceDict.cache.TryGetValue(audio, out var cacheData))
                {
                    var bridge = cacheData.bridge;

                    // Show Phonon's calculated attenuation & occlusion, alongside Unity's blend settings
                    dspStat = $" | Atten: {bridge.CurrentDistanceAttenuation:F2} | Occ: {bridge.CurrentOcclusion:F2} | Blend: {bridge.spatialBlendOverride:F2} | Spatialize: {audio.spatialize}";
                }

                // Construct formatted text
                string text = $"{audio.gameObject.name} [{status}] | Dist: {distance:F1}m | Vol: {baseVol:F2}{dspStat}";

                // Offset each item in the stack
                Vector2 offsetPos = new Vector2(basePos.x, basePos.y + (i * 15));

                // Widened Rect to 800 to accommodate longer debug strings
                DrawOutlinedLabel(new Rect(offsetPos.x, offsetPos.y, 800, 20), text);
            }
        }
    }

    void DrawOutlinedLabel(Rect rect, string text)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);

        // Outline color (black)
        style.normal.textColor = Color.black;

        // Draw outline by offsetting text in 4 directions
        Vector2[] offsets = new Vector2[]
        {
            new Vector2(-1, -1),
            new Vector2(1, -1),
            new Vector2(-1, 1),
            new Vector2(1, 1)
        };

        foreach (var offset in offsets)
        {
            Rect offsetRect = new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
            GUI.Label(offsetRect, text, style);
        }

        // Draw main text (colored)
        style.normal.textColor = Color.white;
        GUI.Label(rect, text, style);
    }
}