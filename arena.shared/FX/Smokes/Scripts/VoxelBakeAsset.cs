using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBakeAsset", menuName = "Arena/Voxel Bake Asset")]
public class VoxelBakeAsset : ScriptableObject
{
    public int[] voxels;
    public int voxelsX;
    public int voxelsY;
    public int voxelsZ;
    public Vector3 boundsExtent;
    public float voxelSize;
}
