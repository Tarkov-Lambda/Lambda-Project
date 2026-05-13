#if EFT_RUNTIME
using EFT;

using EFT.Interactive;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Shared
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
        MeshRenderer _meshRenderer;

        public int NetId { get; }

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
            _meshRenderer = GetComponent<MeshRenderer>();
#if EFT_RUNTIME
            _meshRenderer.enabled = false;
#endif
        }

        private void OnValidate()
        {
            gameObject.layer = 22;
        }
    }
}
