using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Shared
{
    public struct LadderEventPayload
    {
        public Collider other;
        public Ladder ladder;
    }

    public enum LadderMaterial : byte
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
        public static Action<LadderEventPayload> OnPlayerEnterLadder;
        public static Action<LadderEventPayload> OnPlayerExitLadder;

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

        public string Description => "eto ladder kstati btw"; // да ну нахуй кстати

        private void Awake()
        {
            Collider.isTrigger = true; // just in case
        }

        public void OnTriggerEnter(Collider other)
        {
            LadderEventPayload ladderEvent = new()
            {
                other = other,
                ladder = this
            };

            OnPlayerEnterLadder?.Invoke(ladderEvent);
        }

        public void OnTriggerExit(Collider other)
        {
            LadderEventPayload ladderEvent = new()
            {
                other = other,
                ladder = this
            };

            OnPlayerExitLadder?.Invoke(ladderEvent);
        }
    }
}
