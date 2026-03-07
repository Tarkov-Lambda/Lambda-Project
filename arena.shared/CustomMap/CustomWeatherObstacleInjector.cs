using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared.CustomMap
{
    internal class CustomWeatherObstacleInjector : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        private int validMeshCount = 0;

        private void OnValidate()
        {
            MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
            validMeshCount = 0;

            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh != null)
                {
                    validMeshCount++;
                }
            }
        }
#endif

#if EFT_RUNTIME
        private Mesh vanillaMesh;
        private Mesh newCombinedMesh;

        void OnEnable()
        {
            if (WeatherObstacle.Instance == null ||
                WeatherObstacle.Instance.MeshCollider == null ||
                WeatherObstacle.Instance.MeshCollider.sharedMesh == null ||
                DepthPhotograper.Instance == null)
            {
                Debug.LogError("invalid scene");
                return;
            }

            vanillaMesh = WeatherObstacle.Instance.MeshCollider.sharedMesh;
            List<CombineInstance> combineInstances = new List<CombineInstance>();

            combineInstances.Add(new CombineInstance
            {
                mesh = vanillaMesh,
                transform = Matrix4x4.identity
            });

            foreach (MeshFilter filter in gameObject.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                combineInstances.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = filter.transform.localToWorldMatrix
                });
            }

            newCombinedMesh = new Mesh
            {
                name = "WeatherObstacle Custom Combined",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            newCombinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
            WeatherObstacle.Instance.MeshCollider.sharedMesh = newCombinedMesh;

            DepthPhotograper.Instance.Render();
        }

        void OnDisable()
        {
            if (WeatherObstacle.Instance != null && WeatherObstacle.Instance.MeshCollider != null)
                WeatherObstacle.Instance.MeshCollider.sharedMesh = vanillaMesh;

            if (DepthPhotograper.Instance != null)
                DepthPhotograper.Instance.Render();

            if (newCombinedMesh != null)
            {
                Destroy(newCombinedMesh);
                newCombinedMesh = null;
            }
        }
#endif
    }
}