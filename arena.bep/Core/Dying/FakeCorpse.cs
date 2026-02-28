using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.bep.Core.Dying
{
    public class FakeCorpse : MonoBehaviour
    {
        Collider[] cols;

        void Start()
        {
            cols = GetComponentsInChildren<Collider>();

            // disable collisions
            foreach (var col in cols)
            {
                col.isTrigger = true;
            }

            StartCoroutine(Delay());
        }

        // wait one frame (avoid colliding with real body until its teleportation is synced up with physics)
        IEnumerator Delay()
        {
            yield return new WaitForEndOfFrame();

            foreach (var col in cols)
            {
                col.isTrigger = false;
            }
        }

        public void OnRigidbodyStopped()
        {

        }
    }
}
