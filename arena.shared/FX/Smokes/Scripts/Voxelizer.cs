using UnityEngine;

public class Voxelizer : MonoBehaviour
{
    [Header("Bake")]
    public VoxelBakeAsset bakedVoxels;
    public GameObject objectsToVoxelize;

    [Header("Bounds")]
    public Vector3 boundsExtent = new Vector3(3, 3, 3);
    public float voxelSize = 0.25f;

    [Range(0.0f, 2.0f)]
    public float intersectionBias = 1.0f;

    [Header("Smoke")]
    public Vector3 maxRadius = new Vector3(1, 1, 1);

    [Range(0.01f, 5.0f)]
    public float growthSpeed = 1.0f;

    [Range(0, 128)]
    public int maxFillSteps = 16;

    public bool iterateFill = false;
    public bool constantFill = false;

    [Header("Debug")]
    public Mesh debugMesh;
    public bool debugStaticVoxels = false;
    public bool debugSmokeVoxels = false;
    public bool debugEdgeVoxels = false;

    private ComputeBuffer staticVoxelsBuffer, smokeVoxelsBuffer, smokePingVoxelsBuffer, argsBuffer;
    public ComputeShader voxelizeCompute;
    private Material debugVoxelMaterial;
    private Bounds debugBounds;
    private int voxelsX, voxelsY, voxelsZ, totalVoxels;
    private float radius;
    private Vector3 smokeOrigin;

    public ComputeBuffer GetSmokeVoxelBuffer() => smokeVoxelsBuffer;
    public Vector3 GetVoxelResolution() => new Vector3(voxelsX, voxelsY, voxelsZ);
    public Vector3 GetBoundsExtent() => boundsExtent;
    public float GetVoxelSize() => voxelSize;
    public Vector3 GetSmokeOrigin() => smokeOrigin;
    public Vector3 GetSmokeRadius() => Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius));
    public float GetEasing() => Easing(radius);

    void OnEnable()
    {
        if (bakedVoxels == null)
        {
            Debug.LogError("[Voxelizer] No baked voxel asset assigned. Bake the voxels in the editor before entering play mode.", this);
            return;
        }

        radius = 0f;

#if UNITY_EDITOR
        debugVoxelMaterial = new Material(Shader.Find("Hidden/VisualizeVoxels"));
        voxelizeCompute = (ComputeShader)Resources.Load("Voxelize");
#endif

        voxelsX = bakedVoxels.voxelsX;
        voxelsY = bakedVoxels.voxelsY;
        voxelsZ = bakedVoxels.voxelsZ;
        boundsExtent = bakedVoxels.boundsExtent;
        voxelSize = bakedVoxels.voxelSize;
        totalVoxels = voxelsX * voxelsY * voxelsZ;

        Vector3 boundsSize = boundsExtent * 2;
        debugBounds = new Bounds(new Vector3(0, boundsExtent.y, 0), boundsSize);

        // Upload baked static voxels to GPU
        staticVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));
        staticVoxelsBuffer.SetData(bakedVoxels.voxels);

        // Allocate and clear dynamic smoke buffers
        smokeVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));
        smokePingVoxelsBuffer = new ComputeBuffer(totalVoxels, sizeof(int));

        voxelizeCompute.SetBuffer(0, "_Voxels", smokeVoxelsBuffer);
        voxelizeCompute.Dispatch(0, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);
        voxelizeCompute.SetBuffer(0, "_Voxels", smokePingVoxelsBuffer);
        voxelizeCompute.Dispatch(0, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);

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

    float Easing(float x)
    {
        float ease;
        if (x < 0.5f) ease = 2 * x * x;
        else ease = 1.0f - (1.0f / (5.0f * (2.0f * x - 0.8f) + 1));
        return Mathf.Min(1.0f, ease);
    }

    public void SpawnSmoke(Vector3 pos)
    {
        smokeOrigin = pos;
        voxelizeCompute.SetVector("_SmokeOrigin", smokeOrigin);
        radius = 0;
        voxelizeCompute.SetBuffer(0, "_Voxels", smokeVoxelsBuffer);
        voxelizeCompute.Dispatch(0, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);
        voxelizeCompute.Dispatch(2, 1, 1, 1);
    }

    void Update()
    {
        if (staticVoxelsBuffer == null) return;

        voxelizeCompute.SetInt("_MaxFillSteps", maxFillSteps);

        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 50))
            {
                SpawnSmoke(hit.point);
            }
        }

        if (iterateFill || constantFill)
        {
            voxelizeCompute.SetVector("_Radius", Vector3.Lerp(Vector3.zero, maxRadius, Easing(radius)));
            voxelizeCompute.Dispatch(3, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);
            voxelizeCompute.Dispatch(4, Mathf.CeilToInt(totalVoxels / 128.0f), 1, 1);
            iterateFill = false;
            radius += growthSpeed * Time.deltaTime;
        }

        if (debugStaticVoxels || debugSmokeVoxels || debugEdgeVoxels)
        {
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

    void OnDisable()
    {
        staticVoxelsBuffer?.Release();
        smokeVoxelsBuffer?.Release();
        smokePingVoxelsBuffer?.Release();
        argsBuffer?.Release();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 boundsSize = boundsExtent * 2;
        Bounds b = new Bounds(new Vector3(0, boundsExtent.y, 0), boundsSize);
        Gizmos.DrawWireCube(b.center, b.extents * 2);
    }
}
