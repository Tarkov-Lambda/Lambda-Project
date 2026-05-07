using System;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.shared
{
    public struct LadderEventPayload
    {
        public Collider other;
        public Ladder ladder;
    }

    public enum LadderMaterial
    {
        Metal,
        Wood
    }

    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour
#if EFT_RUNTIME
        , IPhysicsTrigger
#endif
    {
        public static Action<LadderEventPayload> onPlayerEnterLadder;
        public static Action<LadderEventPayload> onPlayerExitLadder;

        public LadderMaterial ladderMaterial = LadderMaterial.Metal;

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

        public Vector3 TopPoint
        {
            get
            {
                Bounds b = Collider.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        public Vector3 BottomPoint
        {
            get
            {
                Bounds b = Collider.bounds;
                return new Vector3(b.center.x, b.min.y, b.center.z);
            }
        }

        public string Description => "eto ladder kstati btw";

        private void Awake()
        {
            Collider.isTrigger = true; // just in case
        }

        public void OnTriggerEnter(Collider other)
        {
            LadderEventPayload ladderEvent = new LadderEventPayload
            {
                other = other,
                ladder = this
            };

            onPlayerEnterLadder?.Invoke(ladderEvent);
        }

        public void OnTriggerExit(Collider other)
        {
            LadderEventPayload ladderEvent = new LadderEventPayload
            {
                other = other,
                ladder = this
            };

            onPlayerExitLadder?.Invoke(ladderEvent);
        }
    }
}
