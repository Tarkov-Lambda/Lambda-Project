using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour
    {
        private BoxCollider _collider;

        private BoxCollider Collider
        {
            get
            {
                if (_collider == null)
                    _collider = GetComponent<BoxCollider>();
                return _collider;
            }
        }

        /// <summary>World-space top center of the ladder collider.</summary>
        public Vector3 TopPoint
        {
            get
            {
                Bounds b = Collider.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        /// <summary>World-space bottom center of the ladder collider.</summary>
        public Vector3 BottomPoint
        {
            get
            {
                Bounds b = Collider.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
        }

        private void Awake()
        {
            // Ensure the collider is a trigger so OnTriggerEnter fires
            Collider.isTrigger = true;
        }
    }
}
