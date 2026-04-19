#if EFT_RUNTIME
using EFT;

using EFT.Interactive;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    [RequireComponent(typeof(BoxCollider))]
    public class BombPlantZone :
#if EFT_RUNTIME
        InteractableObject, ILambdaObjective
#else
        MonoBehaviour, ILambdaObjective
#endif
    {
        BoxCollider _boxCollider;

        public string Name { get; }

        public Vector3 Center
        {
            get
            {
                return _boxCollider.center;
            }
        }

        public Bounds Bounds
        {
            get
            {
                return _boxCollider.bounds;
            }
        }

        void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
        }

        private void OnValidate()
        {
            gameObject.layer = 22;
        }
    }
}
