#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool that bakes the static voxel representation of a scene into a
/// VoxelData ScriptableObject asset so that Voxelizer can skip the expensive
/// runtime compute pass and simply upload the pre-baked data to the GPU.
///
/// Usage:  Arena  ▶  Bake Voxels
///
/// The baker will:
///   1. Find the Voxelizer component in the currently open scene.
///   2. Run the same Voxelize.compute dispatch that OnEnable() normally runs.
///   3. Read the resulting staticVoxelsBuffer back to CPU.
///   4. Save (or update) a VoxelData .asset next to Voxelizer.cs.
///   5. Assign the asset to the Voxelizer's bakedVoxelData field and dirty the scene.
/// </summary>
public static class VoxelizerBaker
{
    private const string BakedAssetsDir = "Assets/FX/Smokes/BakedVoxels";

    [MenuItem("Arena/Bake Voxels")]
    public static void BakeVoxels()
    {
        Voxelizer voxelizer = Object.FindObjectOfType<Voxelizer>();
        if (voxelizer == null)
        {
            EditorUtility.DisplayDialog("Voxelizer Baker", "No Voxelizer component found in the active scene.", "OK");
            return;
        }

        if (voxelizer.objectsToVoxelize == null)
        {
            EditorUtility.DisplayDialog("Voxelizer Baker", "Voxelizer.objectsToVoxelize is not assigned.", "OK");
            return;
        }

        // ---------------------------------------------------------------
        // Mirror the compute setup from Voxelizer.OnEnable()
        // ---------------------------------------------------------------
        Vector3 boundsExtent = voxelizer.boundsExtent;
        float   voxelSize    = voxelizer.voxelSize;

        Vector3 boundsSize = boundsExtent * 2f;
        int voxelsX     = Mathf.CeilToInt(boundsSize.x / voxelSize);
        int voxelsY     = Mathf.CeilToInt(boundsSize.y / voxelSize);
        int voxelsZ     = Mathf.CeilToInt(boundsSize.z / voxelSize);
        int totalVoxels = voxelsX * voxelsY * voxelsZ;

        ComputeShader voxelizeCompute = (ComputeShader)Resources.Load("Voxelize");
        if (voxelizeCompute == null)
        {
            EditorUtility.DisplayDialog("Voxelizer Baker", "Could not load Resources/Voxelize compute shader.", "OK");
            return;
        }

        ComputeBuffer staticVoxelsBuffer = new ComputeBuffer(totalVoxels, 4);

        // Kernel 0 — clear
        voxelizeCompute.SetBuffer(0, "_Voxels", staticVoxelsBuffer);
        voxelizeCompute.Dispatch(0, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);

        // Kernel 1 — voxelize each mesh
        foreach (Transform child in voxelizer.objectsToVoxelize.GetComponentsInChildren<Transform>())
        {
            MeshFilter meshFilter = child.gameObject.GetComponent<MeshFilter>();
            if (!meshFilter) continue;

            Mesh sharedMesh = meshFilter.sharedMesh;

            ComputeBuffer verticesBuffer  = new ComputeBuffer(sharedMesh.vertexCount, 3 * sizeof(float));
            ComputeBuffer trianglesBuffer = new ComputeBuffer(sharedMesh.triangles.Length, sizeof(int));

            verticesBuffer.SetData(sharedMesh.vertices);
            trianglesBuffer.SetData(sharedMesh.triangles);

            voxelizeCompute.SetBuffer(1, "_StaticVoxels",        staticVoxelsBuffer);
            voxelizeCompute.SetBuffer(1, "_MeshVertices",        verticesBuffer);
            voxelizeCompute.SetBuffer(1, "_MeshTriangleIndices", trianglesBuffer);
            voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            voxelizeCompute.SetVector("_BoundsExtent",    boundsExtent);
            voxelizeCompute.SetMatrix("_MeshLocalToWorld", child.localToWorldMatrix);
            voxelizeCompute.SetInt("_VoxelCount",     totalVoxels);
            voxelizeCompute.SetInt("_TriangleCount",  sharedMesh.triangles.Length);
            voxelizeCompute.SetFloat("_VoxelSize",         voxelSize);
            voxelizeCompute.SetFloat("_IntersectionBias",  voxelizer.intersectionBias);

            voxelizeCompute.Dispatch(1, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);

            verticesBuffer.Release();
            trianglesBuffer.Release();
        }

        // Read back results
        int[] bakedVoxels = new int[totalVoxels];
        staticVoxelsBuffer.GetData(bakedVoxels);
        staticVoxelsBuffer.Release();

        // ---------------------------------------------------------------
        // Save as VoxelData ScriptableObject
        // ---------------------------------------------------------------
        if (!Directory.Exists(BakedAssetsDir))
            Directory.CreateDirectory(BakedAssetsDir);

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string assetPath = $"{BakedAssetsDir}/{sceneName}_VoxelData.asset";

        VoxelData asset = AssetDatabase.LoadAssetAtPath<VoxelData>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<VoxelData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        asset.voxels      = bakedVoxels;
        asset.resolutionX = voxelsX;
        asset.resolutionY = voxelsY;
        asset.resolutionZ = voxelsZ;
        asset.boundsExtent = boundsExtent;
        asset.voxelSize    = voxelSize;

        EditorUtility.SetDirty(asset);

        // Assign back to the Voxelizer in the scene and dirty the scene
        voxelizer.bakedVoxelData = asset;
        EditorUtility.SetDirty(voxelizer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[VoxelizerBaker] Baked {totalVoxels} voxels ({voxelsX}x{voxelsY}x{voxelsZ}) → {assetPath}");
        EditorUtility.DisplayDialog("Voxelizer Baker",
            $"Baked {totalVoxels:N0} voxels ({voxelsX}×{voxelsY}×{voxelsZ}) successfully.\n\nAsset saved to:\n{assetPath}\n\nRemember to save the scene and include the asset in the map AssetBundle.",
            "OK");
    }
}
#endif
