using UnityEngine;

/// <summary>
/// Serialized asset containing a pre-baked static voxel occupancy grid for a smoke volume.
/// Generated via the "Bake Static Voxels" button on the Voxelizer component.
/// Assign to Voxelizer.bakeData to eliminate GPU mesh voxelization at runtime startup.
/// </summary>
[CreateAssetMenu(fileName = "SmokeVoxelBakeData", menuName = "Arena/Smoke Voxel Bake Data")]
public class SmokeVoxelBakeData : ScriptableObject {
    [HideInInspector] public Vector3    boundsExtent;
    [HideInInspector] public float      voxelSize;
    [HideInInspector] public Vector3Int resolution;

    // Stored as byte[] rather than int[] because static voxels are binary (0 or 1).
    // This reduces serialized asset size by 75% vs. int[].
    [HideInInspector] public byte[] staticVoxelData;

    /// <summary>
    /// Returns true if this bake asset was produced with the same grid parameters
    /// as the Voxelizer currently requesting it. A mismatch means a re-bake is needed.
    /// </summary>
    public bool IsCompatible(Vector3Int res, Vector3 extent, float size) {
        return staticVoxelData != null
            && staticVoxelData.Length > 0
            && resolution      == res
            && boundsExtent    == extent
            && Mathf.Approximately(voxelSize, size);
    }
}
