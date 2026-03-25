#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Voxelizer))]
public class VoxelizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Voxelizer voxelizer = (Voxelizer)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(voxelizer.objectsToVoxelize == null))
        {
            if (GUILayout.Button("Bake"))
                Bake(voxelizer);
        }

        using (new EditorGUI.DisabledScope(voxelizer.bakedVoxels == null))
        {
            if (GUILayout.Button("Clear Bake"))
                ClearBake(voxelizer);
        }

        if (voxelizer.objectsToVoxelize == null)
            EditorGUILayout.HelpBox("Assign Objects To Voxelize to enable baking.", MessageType.Warning);

        if (voxelizer.bakedVoxels != null)
        {
            int total = voxelizer.bakedVoxels.voxelsX * voxelizer.bakedVoxels.voxelsY * voxelizer.bakedVoxels.voxelsZ;
            EditorGUILayout.HelpBox(
                $"Baked: {voxelizer.bakedVoxels.voxelsX} x {voxelizer.bakedVoxels.voxelsY} x {voxelizer.bakedVoxels.voxelsZ}  ({total} voxels)",
                MessageType.Info);
        }
    }

    static void Bake(Voxelizer voxelizer)
    {
        ComputeShader compute = (ComputeShader)Resources.Load("Voxelize");
        if (compute == null)
        {
            Debug.LogError("[VoxelizerEditor] Could not load Voxelize compute shader from Resources.");
            return;
        }

        Vector3 boundsExtent = voxelizer.boundsExtent;
        float voxelSize = voxelizer.voxelSize;
        Vector3 boundsSize = boundsExtent * 2;

        int voxelsX = Mathf.CeilToInt(boundsSize.x / voxelSize);
        int voxelsY = Mathf.CeilToInt(boundsSize.y / voxelSize);
        int voxelsZ = Mathf.CeilToInt(boundsSize.z / voxelSize);
        int totalVoxels = voxelsX * voxelsY * voxelsZ;

        ComputeBuffer staticVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));

        // Kernel 0: clear
        compute.SetBuffer(0, "_Voxels", staticVoxelsBuffer);
        compute.Dispatch(0, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);

        // Kernel 1: voxelize mesh geometry
        foreach (Transform child in voxelizer.objectsToVoxelize.GetComponentsInChildren<Transform>())
        {
            MeshFilter meshFilter = child.gameObject.GetComponent<MeshFilter>();
            if (!meshFilter) continue;

            Mesh mesh = meshFilter.sharedMesh;

            ComputeBuffer verticesBuffer = new ComputeBuffer(mesh.vertexCount, 3 * sizeof(float));
            verticesBuffer.SetData(mesh.vertices);
            ComputeBuffer trianglesBuffer = new ComputeBuffer(mesh.triangles.Length, sizeof(int));
            trianglesBuffer.SetData(mesh.triangles);

            compute.SetBuffer(1, "_StaticVoxels", staticVoxelsBuffer);
            compute.SetBuffer(1, "_MeshVertices", verticesBuffer);
            compute.SetBuffer(1, "_MeshTriangleIndices", trianglesBuffer);
            compute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            compute.SetVector("_BoundsExtent", boundsExtent);
            compute.SetMatrix("_MeshLocalToWorld", child.localToWorldMatrix);
            compute.SetInt("_VoxelCount", totalVoxels);
            compute.SetInt("_TriangleCount", mesh.triangles.Length);
            compute.SetFloat("_VoxelSize", voxelSize);
            compute.SetFloat("_IntersectionBias", voxelizer.intersectionBias);
            compute.Dispatch(1, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);

            verticesBuffer.Release();
            trianglesBuffer.Release();
        }

        // Readback
        int[] voxelData = new int[totalVoxels];
        staticVoxelsBuffer.GetData(voxelData);
        staticVoxelsBuffer.Release();

        // Save asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Voxel Bake Asset",
            "VoxelBakeAsset",
            "asset",
            "Choose where to save the baked voxel data.");

        if (string.IsNullOrEmpty(path)) return;

        VoxelBakeAsset asset = AssetDatabase.LoadAssetAtPath<VoxelBakeAsset>(path)
                              ?? ScriptableObject.CreateInstance<VoxelBakeAsset>();

        asset.voxels = voxelData;
        asset.voxelsX = voxelsX;
        asset.voxelsY = voxelsY;
        asset.voxelsZ = voxelsZ;
        asset.boundsExtent = boundsExtent;
        asset.voxelSize = voxelSize;

        if (!AssetDatabase.Contains(asset))
            AssetDatabase.CreateAsset(asset, path);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(voxelizer, "Bake Voxels");
        voxelizer.bakedVoxels = asset;
        EditorUtility.SetDirty(voxelizer);

        Debug.Log($"[VoxelizerEditor] Baked {totalVoxels} voxels ({voxelsX}x{voxelsY}x{voxelsZ}) → {path}");
    }

    static void ClearBake(Voxelizer voxelizer)
    {
        if (!EditorUtility.DisplayDialog(
                "Clear Bake",
                "Remove the baked voxel asset reference? The asset file will not be deleted.",
                "Clear", "Cancel"))
            return;

        Undo.RecordObject(voxelizer, "Clear Voxel Bake");
        voxelizer.bakedVoxels = null;
        EditorUtility.SetDirty(voxelizer);
    }
}
#endif
