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
    public class MapSelector :
#if EFT_RUNTIME
        InteractableObject
#else
        MonoBehaviour
#endif
    {
        BoxCollider _boxCollider;
        MeshRenderer _meshRenderer;

        public Vector3 Center
        {
            get => _boxCollider.center;
        }

        public Bounds Bounds
        {
            get => _boxCollider.bounds;
        }

        void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            _meshRenderer = GetComponent<MeshRenderer>();
#if EFT_RUNTIME
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
#endif
        }

        private void OnValidate()
        {
            gameObject.layer = 22;
        }
    }
}
