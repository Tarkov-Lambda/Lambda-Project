using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    public class SpawnPoints : MonoBehaviour
    {
        [SerializeField]
        public Faction faction;

        private void OnDrawGizmos()
        {
            foreach (Transform child in transform)
            {

                Gizmos.DrawCube(child.position, Vector3.one * 0.1f);
            }
        }
    }
}
