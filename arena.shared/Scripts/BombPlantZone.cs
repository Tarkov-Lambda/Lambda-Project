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
        InteractableObject
#else
        MonoBehaviour
#endif
    {
        private void OnValidate()
        {
            gameObject.layer = 22;
        }
    }
}
