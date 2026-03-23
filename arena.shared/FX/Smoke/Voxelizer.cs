using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Voxelizer : MonoBehaviour {
    public Vector3 boundsExtent = new Vector3(3, 3, 3);

    public float voxelSize = 0.25f;

    public GameObject objectsToVoxelize = null;

    [Range(0.0f, 2.0f)]
    public float intersectionBias = 1.0f;

    public Mesh debugMesh;

    public bool debugStaticVoxels = false;
    public bool debugSmokeVoxels = false;
    public bool debugEdgeVoxels = false;

    public Vector3 maxRadius = new Vector3(1, 1, 1);

    [Range(0.01f, 5.0f)]
    public float growthSpeed = 1.0f;

    [Range(0, 128)]
    public int maxFillSteps = 16;

    public bool iterateFill = false;
    public bool constantFill = false;

    [Header("Bake Settings")]
    [Space(5)]
    [Tooltip("Pre-baked static voxel data. Assign this to skip live GPU voxelization at startup. Use 'Bake Static Voxels' in the Inspector or right-click context menu to generate.")]
    public SmokeVoxelBakeData bakeData = null;

    private ComputeBuffer staticVoxelsBuffer, smokeVoxelsBuffer, smokePingVoxelsBuffer, argsBuffer;
    private ComputeShader voxelizeCompute;
    private Material debugVoxelMaterial;
    private Bounds debugBounds;
    private int voxelsX, voxelsY, voxelsZ, totalVoxels;
    private float radius;
    private Vector3 smokeOrigin;

    public ComputeBuffer GetSmokeVoxelBuffer() {
        return smokeVoxelsBuffer;
    }

    public Vector3 GetVoxelResolution() {
        return new Vector3(voxelsX, voxelsY, voxelsZ);
    }

    public Vector3 GetBoundsExtent() {
        return boundsExtent;
    }

    public float GetVoxelSize() {
        return voxelSize;
    }

    public Vector3 GetSmokeOrigin() {
        return smokeOrigin;
    }

    public Vector3 GetSmokeRadius() {
        return Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius));
    }

    public float GetEasing() {
        return Easing(radius);
    }

    // Dispatches the compute kernel across the full voxel grid using a 3D thread group layout
    // [numthreads(4,4,8)]. Each axis group count stays well below the DX11 cap of 65535,
    // regardless of how large the voxel grid is.
    void DispatchFull(int kernel) {
        voxelizeCompute.Dispatch(kernel,
            Mathf.CeilToInt(voxelsX / 4.0f),
            Mathf.CeilToInt(voxelsY / 4.0f),
            Mathf.CeilToInt(voxelsZ / 8.0f));
    }

    void RunMeshVoxelization() {
        foreach (Transform child in objectsToVoxelize.GetComponentsInChildren<Transform>()) {
            MeshFilter meshFilter = child.gameObject.GetComponent<MeshFilter>();

            if (!meshFilter) continue;
            Mesh sharedMesh = meshFilter.sharedMesh;

            ComputeBuffer verticesBuffer = new ComputeBuffer(sharedMesh.vertexCount, 3 * sizeof(float));
            verticesBuffer.SetData(sharedMesh.vertices);
            ComputeBuffer trianglesBuffer = new ComputeBuffer(sharedMesh.triangles.Length, sizeof(int));
            trianglesBuffer.SetData(sharedMesh.triangles);

            voxelizeCompute.SetBuffer(1, "_StaticVoxels", staticVoxelsBuffer);
            voxelizeCompute.SetBuffer(1, "_MeshVertices", verticesBuffer);
            voxelizeCompute.SetBuffer(1, "_MeshTriangleIndices", trianglesBuffer);
            voxelizeCompute.SetMatrix("_MeshLocalToWorld", child.localToWorldMatrix);
            voxelizeCompute.SetInt("_TriangleCount", sharedMesh.triangles.Length);
            voxelizeCompute.SetFloat("_IntersectionBias", intersectionBias);

            DispatchFull(1);

            verticesBuffer.Release();
            trianglesBuffer.Release();
        }
    }

    void OnEnable() {
        radius = 0.0f;
        debugVoxelMaterial = new Material(Shader.Find("Hidden/VisualizeVoxels"));
        voxelizeCompute = (ComputeShader)Resources.Load("Voxelize");

        Vector3 boundsSize = boundsExtent * 2;
        debugBounds = new Bounds(new Vector3(0, boundsExtent.y, 0), boundsSize);

        voxelsX = Mathf.CeilToInt(boundsSize.x / voxelSize);
        voxelsY = Mathf.CeilToInt(boundsSize.y / voxelSize);
        voxelsZ = Mathf.CeilToInt(boundsSize.z / voxelSize);
        totalVoxels = voxelsX * voxelsY * voxelsZ;

        // Set resolution uniforms before any dispatch — CS_Clear's bounds guard depends on them
        voxelizeCompute.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
        voxelizeCompute.SetVector("_BoundsExtent", boundsExtent);
        voxelizeCompute.SetFloat("_VoxelSize", voxelSize);

        staticVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));

        // Clear static buffer
        voxelizeCompute.SetBuffer(0, "_Voxels", staticVoxelsBuffer);
        DispatchFull(0);

        Vector3Int res = new Vector3Int(voxelsX, voxelsY, voxelsZ);
        if (bakeData != null && bakeData.IsCompatible(res, boundsExtent, voxelSize)) {
            // ── Fast path: upload pre-baked static geometry voxels directly ──────────
            // No GPU voxelization, no mesh data required at runtime.
            int[] upload = new int[totalVoxels];
            for (int i = 0; i < totalVoxels; i++)
                upload[i] = bakeData.staticVoxelData[i];
            staticVoxelsBuffer.SetData(upload);
        } else {
            // ── Slow path: live GPU mesh voxelization ────────────────────────────────
            // Only runs in-editor; stripped from non-editor builds.
#if UNITY_EDITOR
            if (objectsToVoxelize != null) {
                RunMeshVoxelization();
                Debug.LogWarning("[Voxelizer] Running live mesh voxelization — bake static voxels before shipping.");
            } else {
                Debug.LogWarning("[Voxelizer] No bake data and no objectsToVoxelize assigned. Static voxels will be empty.");
            }
#else
            Debug.LogError("[Voxelizer] No valid SmokeVoxelBakeData assigned. Static smoke occlusion is non-functional in this build. Bake and assign the data asset.");
#endif
        }

        smokeVoxelsBuffer    = new ComputeBuffer(totalVoxels, sizeof(int));
        smokePingVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));

        // Clear smoke and ping buffers
        voxelizeCompute.SetBuffer(0, "_Voxels", smokeVoxelsBuffer);
        DispatchFull(0);
        voxelizeCompute.SetBuffer(0, "_Voxels", smokePingVoxelsBuffer);
        DispatchFull(0);

        // Bind buffers for all kernels that need them (indices are stable — never re-bound unless buffer is recreated)
        voxelizeCompute.SetBuffer(2, "_SmokeVoxels", smokeVoxelsBuffer);

        voxelizeCompute.SetBuffer(3, "_StaticVoxels", staticVoxelsBuffer);
        voxelizeCompute.SetBuffer(3, "_SmokeVoxels", smokeVoxelsBuffer);
        voxelizeCompute.SetBuffer(3, "_PingVoxels", smokePingVoxelsBuffer);

        voxelizeCompute.SetBuffer(4, "_Voxels", smokeVoxelsBuffer);
        voxelizeCompute.SetBuffer(4, "_PingVoxels", smokePingVoxelsBuffer);
        voxelizeCompute.SetBuffer(4, "_StaticVoxels", staticVoxelsBuffer);

        // Debug instancing args
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)debugMesh.GetIndexCount(0);
        args[1] = (uint)totalVoxels;
        args[2] = (uint)debugMesh.GetIndexStart(0);
        args[3] = (uint)debugMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Runs the full GPU mesh voxelization pipeline, reads back the result to the CPU,
    /// and serializes it into a SmokeVoxelBakeData ScriptableObject asset.
    /// Assign the resulting asset to the bakeData field to eliminate runtime voxelization.
    /// </summary>
    [ContextMenu("Bake Static Voxels")]
    public void BakeStaticVoxels() {
        if (objectsToVoxelize == null) {
            Debug.LogError("[Voxelizer] Cannot bake: objectsToVoxelize is null.");
            return;
        }

        ComputeShader compute = (ComputeShader)Resources.Load("Voxelize");

        Vector3 boundsSize = boundsExtent * 2;
        int bakeX = Mathf.CeilToInt(boundsSize.x / voxelSize);
        int bakeY = Mathf.CeilToInt(boundsSize.y / voxelSize);
        int bakeZ = Mathf.CeilToInt(boundsSize.z / voxelSize);
        int bakeTotalVoxels = bakeX * bakeY * bakeZ;

        compute.SetVector("_VoxelResolution", new Vector3(bakeX, bakeY, bakeZ));
        compute.SetVector("_BoundsExtent", boundsExtent);
        compute.SetFloat("_VoxelSize", voxelSize);

        ComputeBuffer bakeBuffer = new ComputeBuffer(bakeTotalVoxels, sizeof(int));

        int gx = Mathf.CeilToInt(bakeX / 4.0f);
        int gy = Mathf.CeilToInt(bakeY / 4.0f);
        int gz = Mathf.CeilToInt(bakeZ / 8.0f);

        // Clear
        compute.SetBuffer(0, "_Voxels", bakeBuffer);
        compute.Dispatch(0, gx, gy, gz);

        // Voxelize each mesh in the scene
        foreach (Transform child in objectsToVoxelize.GetComponentsInChildren<Transform>()) {
            MeshFilter meshFilter = child.gameObject.GetComponent<MeshFilter>();
            if (!meshFilter) continue;
            Mesh sharedMesh = meshFilter.sharedMesh;

            ComputeBuffer verticesBuffer  = new ComputeBuffer(sharedMesh.vertexCount, 3 * sizeof(float));
            verticesBuffer.SetData(sharedMesh.vertices);
            ComputeBuffer trianglesBuffer = new ComputeBuffer(sharedMesh.triangles.Length, sizeof(int));
            trianglesBuffer.SetData(sharedMesh.triangles);

            compute.SetBuffer(1, "_StaticVoxels", bakeBuffer);
            compute.SetBuffer(1, "_MeshVertices", verticesBuffer);
            compute.SetBuffer(1, "_MeshTriangleIndices", trianglesBuffer);
            compute.SetMatrix("_MeshLocalToWorld", child.localToWorldMatrix);
            compute.SetInt("_TriangleCount", sharedMesh.triangles.Length);
            compute.SetFloat("_IntersectionBias", intersectionBias);

            compute.Dispatch(1, gx, gy, gz);

            verticesBuffer.Release();
            trianglesBuffer.Release();
        }

        // Blocking CPU-GPU readback — acceptable in editor, never in a shipped build
        int[] readback = new int[bakeTotalVoxels];
        bakeBuffer.GetData(readback);
        bakeBuffer.Release();

        // Compress int[] → byte[]: static voxels are binary (0 or 1), so byte is sufficient.
        // This cuts the serialized asset size by 75% compared to storing as int[].
        byte[] compressed = new byte[bakeTotalVoxels];
        for (int i = 0; i < bakeTotalVoxels; i++)
            compressed[i] = (byte)readback[i];

        // Create or update the ScriptableObject asset
        string assetPath = bakeData != null
            ? UnityEditor.AssetDatabase.GetAssetPath(bakeData)
            : $"Assets/SmokeVoxelBakeData_{name}.asset";

        SmokeVoxelBakeData data = bakeData ?? ScriptableObject.CreateInstance<SmokeVoxelBakeData>();
        data.boundsExtent    = boundsExtent;
        data.voxelSize       = voxelSize;
        data.resolution      = new Vector3Int(bakeX, bakeY, bakeZ);
        data.staticVoxelData = compressed;

        if (bakeData == null) {
            UnityEditor.AssetDatabase.CreateAsset(data, assetPath);
            bakeData = data;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        UnityEditor.EditorUtility.SetDirty(data);
        UnityEditor.AssetDatabase.SaveAssets();

        Debug.Log($"[Voxelizer] Bake complete — {bakeTotalVoxels:N0} voxels ({compressed.Length / 1024:N0} KB) → {assetPath}");
    }
#endif

    float Easing(float x) {
        float ease = 0.0f;

        if (x < 0.5f) ease = 2 * x * x;
        else ease = 1.0f - (1.0f / (5.0f * (2.0f * x - 0.8f) + 1));

        return Mathf.Min(1.0f, ease);
    }

    void Update() {
        voxelizeCompute.SetInt("_MaxFillSteps", maxFillSteps);

        if (Input.GetMouseButtonDown(2)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 50)) {
                smokeOrigin = hit.point;
                voxelizeCompute.SetVector("_SmokeOrigin", smokeOrigin);

                radius = 0;
                voxelizeCompute.SetBuffer(0, "_Voxels", smokeVoxelsBuffer);
                DispatchFull(0);

                voxelizeCompute.Dispatch(2, 1, 1, 1);
            }
        }

        if (iterateFill || constantFill) {
            voxelizeCompute.SetVector("_Radius", Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius)));

            DispatchFull(3);
            DispatchFull(4);

            iterateFill = false;
            radius += growthSpeed * Time.deltaTime;
        }

        if (debugStaticVoxels || debugSmokeVoxels || debugEdgeVoxels) {
            debugVoxelMaterial.SetBuffer("_StaticVoxels", staticVoxelsBuffer);
            debugVoxelMaterial.SetBuffer("_SmokeVoxels", smokeVoxelsBuffer);
            debugVoxelMaterial.SetVector("_VoxelResolution", new Vector3(voxelsX, voxelsY, voxelsZ));
            debugVoxelMaterial.SetVector("_BoundsExtent", boundsExtent);
            debugVoxelMaterial.SetFloat("_VoxelSize", voxelSize);
            debugVoxelMaterial.SetInt("_MaxFillSteps", maxFillSteps);
            debugVoxelMaterial.SetInt("_DebugSmokeVoxels", debugSmokeVoxels ? 1 : 0);
            debugVoxelMaterial.SetInt("_DebugStaticVoxels", debugStaticVoxels ? 1 : 0);

            Graphics.DrawMeshInstancedIndirect(debugMesh, 0, debugVoxelMaterial, debugBounds, argsBuffer);
        }
    }

    void OnDisable() {
        staticVoxelsBuffer.Release();
        smokeVoxelsBuffer.Release();
        smokePingVoxelsBuffer.Release();
        argsBuffer.Release();
    }

    void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(debugBounds.center, debugBounds.extents * 2);
    }
}
