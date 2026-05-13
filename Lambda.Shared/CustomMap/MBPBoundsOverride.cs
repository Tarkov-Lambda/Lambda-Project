using System;
using UnityEngine;

namespace Lambda.Shared.CustomMap
{
    public class MBPBoundsOverride : MonoBehaviour
    {
        [SerializeField]
        private Bounds _mbpBounds = new Bounds(Vector3.zero, Vector3.one * 1000);

        [Range(1f, 16f)]
        [SerializeField]
        private int _mbpSubdivisions = 16;

        void OnEnable()
        {
            Bounds bounds = GetGlobalBounds();
            int num = _mbpSubdivisions;
            Physics.RebuildBroadphaseRegions(bounds, _mbpSubdivisions);
            Debug.LogFormat("MBPBounds overriden: {0} {1}", new object[] { bounds, _mbpSubdivisions });
        }

        private Bounds GetGlobalBounds()
        {
            Bounds mbpBounds = _mbpBounds;
            mbpBounds.center = transform.TransformPoint(_mbpBounds.center);
            return mbpBounds;
        }

        private void OnDrawGizmosSelected()
        {
            Bounds b = GetGlobalBounds();

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(b.center, b.size);

            if (_mbpSubdivisions > 1)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f);

                Vector3 min = b.min;
                Vector3 max = b.max;
                float stepX = b.size.x / _mbpSubdivisions;
                float stepZ = b.size.z / _mbpSubdivisions;

                for (int i = 1; i < _mbpSubdivisions; i++)
                {
                    float xPos = min.x + (stepX * i);
                    Gizmos.DrawLine(new Vector3(xPos, min.y, min.z), new Vector3(xPos, min.y, max.z));
                    Gizmos.DrawLine(new Vector3(xPos, max.y, min.z), new Vector3(xPos, max.y, max.z));
                    Gizmos.DrawLine(new Vector3(xPos, min.y, min.z), new Vector3(xPos, max.y, min.z));
                    Gizmos.DrawLine(new Vector3(xPos, min.y, max.z), new Vector3(xPos, max.y, max.z));
                }

                for (int i = 1; i < _mbpSubdivisions; i++)
                {
                    float zPos = min.z + (stepZ * i);
                    Gizmos.DrawLine(new Vector3(min.x, min.y, zPos), new Vector3(max.x, min.y, zPos));
                    Gizmos.DrawLine(new Vector3(min.x, max.y, zPos), new Vector3(max.x, max.y, zPos));
                    Gizmos.DrawLine(new Vector3(min.x, min.y, zPos), new Vector3(min.x, max.y, zPos));
                    Gizmos.DrawLine(new Vector3(max.x, min.y, zPos), new Vector3(max.x, max.y, zPos));
                }
            }
        }
    }
}
