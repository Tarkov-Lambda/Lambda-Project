using Comfort.Common;
using System.Collections.Generic;
using UnityEngine;

namespace ifp.arena.bep.networking
{
    public class NetworkedPhysicsObject : MonoBehaviour
    {
        // Global registry to lookup objects by ID
        public static Dictionary<string, NetworkedPhysicsObject> Registry = new Dictionary<string, NetworkedPhysicsObject>();

        public string ObjectId; // Set this unique per object (hash of name, or manual ID)
        public Rigidbody Rb;

        // Interpolation variables
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _lastReceiveTime;
        private bool _isInterpolating = false;

        // Threshold to avoid spamming packets
        private Vector3 _lastSentPosition;
        private Quaternion _lastSentRotation;
        private const float MoveThreshold = 0.05f;
        private const float AngleThreshold = 1.0f;

        private void Awake()
        {
            if (Rb == null) Rb = GetComponent<Rigidbody>();

            // Register object
            if (!Registry.ContainsKey(ObjectId))
            {
                Registry.Add(ObjectId, this);
            }

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _lastSentPosition = transform.position;
            _lastSentRotation = transform.rotation;
        }

        private void OnDestroy()
        {
            if (Registry.ContainsKey(ObjectId))
            {
                Registry.Remove(ObjectId);
            }
        }

        public void UpdateNetworkState(Vector3 pos, Quaternion rot)
        {
            _targetPosition = pos;
            _targetRotation = rot;
            _lastReceiveTime = Time.time;
            _isInterpolating = true;

            // OPTIONAL: If the object is being moved by network, 
            // you might want to temporarily make it Kinematic so local physics doesn't fight it.
            // Rb.isKinematic = true; 
        }

        private void Update()
        {
            // 1. Handle Interpolation (Smoothing)
            if (_isInterpolating)
            {
                float t = (Time.time - _lastReceiveTime) / 0.1f; // 0.1f is rough packet interval
                transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
                transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);

                // Stop interpolating if we are close enough
                if (Vector3.Distance(transform.position, _targetPosition) < 0.01f)
                {
                    _isInterpolating = false;
                    // Rb.isKinematic = false; // Restore physics
                }
            }
        }

        private void FixedUpdate()
        {
            // 2. Handle Sending (Authority check)
            // Logic: If we moved significantly since last send, send a packet.
            // Note: In a real scenario, you only want the "Owner" to send. 
            // If both Client and Server can move it, we assume whoever moved it takes ownership.

            if (!_isInterpolating) // Only send if we aren't currently being controlled by the network
            {
                bool moved = Vector3.Distance(transform.position, _lastSentPosition) > MoveThreshold;
                bool rotated = Quaternion.Angle(transform.rotation, _lastSentRotation) > AngleThreshold;

                if (moved || rotated)
                {
                    // Call your packet handler here to send data
                    // You will need a reference to your PacketHandler singleton/instance
                    Singleton<ObjectTransformPacketHandler>.Instance.Send(this.gameObject, ObjectId);

                    _lastSentPosition = transform.position;
                    _lastSentRotation = transform.rotation;
                }
            }
        }
    }
}