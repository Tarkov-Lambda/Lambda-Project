#if EFT_RUNTIME
using EFT;

using EFT.Interactive;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    public struct HardpointEventPayload
    {
        public Collider other;
        public HardpointZone hardpoint;
    }

    [RequireComponent(typeof(BoxCollider))]
    public class HardpointZone : MonoBehaviour, ILambdaObjective
    {
        public static Action<HardpointEventPayload> onPlayerEnterLadder;
        public static Action<HardpointEventPayload> onPlayerExitLadder;

        BoxCollider _boxCollider;

        public int NetId { get; }

        public List<int> playerIdsInZone = new List<int>();

        public ZoneOwnership ZoneOwnership { get; private set; } = ZoneOwnership.None;

        public void ChangeOwnership(ZoneOwnership ownership)
        {
            ZoneOwnership = ownership;
        }

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
            gameObject.layer = 18;
            _boxCollider.isTrigger = true;
        }
    }
}
