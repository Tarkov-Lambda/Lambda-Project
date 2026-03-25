using UnityEngine;

/// <summary>
/// Serializable container for pre-baked static voxel data produced by VoxelizerBaker.
/// Include this asset in the same AssetBundle as the scene that contains the Voxelizer
/// component so that it is available at load time without any runtime compute work.
/// </summary>
[CreateAssetMenu(fileName = "VoxelData", menuName = "Arena/Voxel Data")]
public class VoxelData : ScriptableObject
{
    /// <summary>Flat packed voxel occupancy array (1 int per voxel).</summary>
    public int[] voxels;

    public int resolutionX;
    public int resolutionY;
    public int resolutionZ;

    public Vector3 boundsExtent;
    public float voxelSize;
}
