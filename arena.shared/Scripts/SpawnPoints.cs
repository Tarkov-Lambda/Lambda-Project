using UnityEngine;

namespace ifp.arena.shared
{
    public class SpawnPointCluster : MonoBehaviour
    {
        [SerializeField]
        public Faction faction;

        public int pairId;

        public Vector3 GetRandomSpawn()
        {
            int count = transform.childCount;
            int index = Random.Range(0, count);
            return transform.GetChild(index).position;
        }

        private void OnDrawGizmos()
        {
            foreach (Transform child in transform)
            {
                Gizmos.DrawCube(child.position, Vector3.one * 0.1f);
            }
        }
    }
}
