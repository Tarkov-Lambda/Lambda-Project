using UnityEngine;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lambda.Shared
{
    [RequireComponent(typeof(BoxCollider))]
    public class SpawnPointCluster : MonoBehaviour
    {
        [SerializeField] public Faction faction;
        public int pairId;

        [Header("Generation")]
        [SerializeField] private GameObject spawnPointPrefab;
        [SerializeField] private int spawnPointCount = 8;
        [SerializeField] private Transform lookAtTarget;

        private readonly float minSpawnDistance = 2f;
        private readonly int maxPlacementAttempts = 50;

        BoxCollider box;

        public Transform GetRandomSpawn()
        {
            int count = transform.childCount;

            if (count == 0)
                return transform;

            int index = Random.Range(0, count);
            return transform.GetChild(index);
        }

        void Awake()
        {
            box = GetComponent<BoxCollider>();
#if EFT_RUNTIME
            box.enabled = false;
#endif
        }

        public void GenerateSpawnPoints()
        {
            box = GetComponent<BoxCollider>();

            if (box == null)
            {
                Debug.LogWarning("SpawnPointCluster requires a BoxCollider.");
                return;
            }

            if (spawnPointPrefab == null)
            {
                Debug.LogWarning("Spawn point prefab is missing.");
                return;
            }

            ClearSpawnPoints();

            Bounds bounds = box.bounds;
            int layerMask = 1 << 18;

            var placedPositions = new List<Vector3>();

            for (int i = 0; i < spawnPointCount; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
                {
                    Vector3 origin = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        bounds.max.y + 0.05f,
                        Random.Range(bounds.min.z, bounds.max.z)
                    );

                    if (!Physics.Raycast(
                            origin,
                            Vector3.down,
                            out RaycastHit hit,
                            bounds.size.y + 10f,
                            layerMask))
                    {
                        continue;
                    }

                    float spawnOffset = 0.25f;
                    Vector3 candidatePosition = hit.point + hit.normal * spawnOffset;

                    bool tooClose = false;

                    for (int j = 0; j < placedPositions.Count; j++)
                    {
                        if (Vector3.Distance(candidatePosition, placedPositions[j]) < minSpawnDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                        continue;

#if UNITY_EDITOR
                    GameObject spawn;

                    if (!Application.isPlaying)
                    {
                        spawn = (GameObject)PrefabUtility.InstantiatePrefab(spawnPointPrefab, transform);
                    }
                    else
                    {
                        spawn = Instantiate(spawnPointPrefab, transform);
                    }
#else
            GameObject spawn = Instantiate(spawnPointPrefab, transform);
#endif

                    Quaternion rotation = Quaternion.identity;

                    if (lookAtTarget != null)
                    {
                        Vector3 flatDirection = lookAtTarget.position - candidatePosition;
                        flatDirection.y = 0f;

                        if (flatDirection.sqrMagnitude > 0.0001f) rotation = Quaternion.LookRotation(flatDirection);
                    }

                    spawn.transform.SetPositionAndRotation(candidatePosition, rotation);
                    
                    spawn.name = $"SpawnPoint_{i}";

                    placedPositions.Add(candidatePosition);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    Debug.LogWarning($"Could only place {placedPositions.Count} spawn points. Increase cluster size or reduce minSpawnDistance.");
                    break;
                }
            }
        }

        public void ClearSpawnPoints()
        {
#if UNITY_EDITOR
            while (transform.childCount > 0)
            {
                if (!Application.isPlaying)
                    DestroyImmediate(transform.GetChild(0).gameObject);
                else
                    Destroy(transform.GetChild(0).gameObject);
            }
#else
            while (transform.childCount > 0)
            {
                Destroy(transform.GetChild(0).gameObject);
            }
#endif
        }

        private void OnDrawGizmos()
        {
            foreach (Transform child in transform)
            {
                Gizmos.DrawCube(child.position, Vector3.one * 0.1f);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SpawnPointCluster))]
    public class SpawnPointClusterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SpawnPointCluster cluster = (SpawnPointCluster)target;

            GUILayout.Space(8);

            if (GUILayout.Button("Generate Spawn Points"))
            {
                cluster.GenerateSpawnPoints();
                EditorUtility.SetDirty(cluster);
            }

            if (GUILayout.Button("Clear Spawn Points"))
            {
                cluster.ClearSpawnPoints();
                EditorUtility.SetDirty(cluster);
            }
        }
    }
#endif
}