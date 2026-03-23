#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for Voxelizer that surfaces the bake workflow.
/// Strips entirely from non-editor builds via the Editor-only assembly definition.
/// </summary>
[CustomEditor(typeof(Voxelizer))]
public class VoxelizerBaker : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Bake Tools", EditorStyles.boldLabel);

        Voxelizer voxelizer = (Voxelizer)target;

        bool hasBakeData = voxelizer.bakeData != null;
        bool isCompatible = false;

        if (hasBakeData) {
            Vector3 boundsSize = voxelizer.boundsExtent * 2;
            int rx = Mathf.CeilToInt(boundsSize.x / voxelizer.voxelSize);
            int ry = Mathf.CeilToInt(boundsSize.y / voxelizer.voxelSize);
            int rz = Mathf.CeilToInt(boundsSize.z / voxelizer.voxelSize);
            isCompatible = voxelizer.bakeData.IsCompatible(
                new Vector3Int(rx, ry, rz),
                voxelizer.boundsExtent,
                voxelizer.voxelSize);
        }

        // Status indicator
        if (!hasBakeData) {
            EditorGUILayout.HelpBox(
                "No bake data assigned. Runtime will fall back to live GPU voxelization (editor only) or fail in builds.",
                MessageType.Warning);
        } else if (!isCompatible) {
            EditorGUILayout.HelpBox(
                "Bake data exists but is stale — voxelSize or boundsExtent has changed. Re-bake before building.",
                MessageType.Warning);
        } else {
            int totalVoxels = voxelizer.bakeData.staticVoxelData?.Length ?? 0;
            EditorGUILayout.HelpBox(
                $"Bake data valid. {totalVoxels:N0} voxels  ({totalVoxels / 1024:N0} KB).",
                MessageType.Info);
        }

        EditorGUILayout.Space(4);

        bool canBake = voxelizer.objectsToVoxelize != null;
        using (new EditorGUI.DisabledScope(!canBake)) {
            if (GUILayout.Button("Bake Static Voxels", GUILayout.Height(28))) {
                voxelizer.BakeStaticVoxels();
            }
        }

        if (!canBake) {
            EditorGUILayout.HelpBox(
                "Assign 'Objects To Voxelize' to enable baking.",
                MessageType.None);
        }
    }
}
#endif
