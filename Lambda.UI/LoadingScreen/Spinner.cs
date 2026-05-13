using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.UI
{
    public class Spinner : MonoBehaviour
    {
        [SerializeField] private float speed = -50f;

        void Start()
        {
        
        }

        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        void Update()
        {
            transform.Rotate(new Vector3(0, 0, Time.deltaTime * speed));
        }
    }
}
