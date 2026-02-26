using EFT;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    public class SpawnPoints : MonoBehaviour
    {
        public Faction spawnType;

        public Vector3[] GetPositions()
        {
            var list = new List<Vector3>();
            foreach (Transform transform in transform)
            {
                list.Add(transform.position);
            }

            return list.ToArray();
        }
    }
}
