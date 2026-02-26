using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

using CorpseRagdoll = RagdollClass;

namespace ifp.arena.bep
{
    public static class RagdollCreator
    {
        public static void CreateRagdollFromPlayer(Player player)
        {
            //new CorpseRagdoll();

            CloneWithSpecificComponents(player.gameObject,
                typeof(RigidbodySpawner),
                typeof(PlayerBody),
                typeof(Collider),
                typeof(CharacterJointSpawner),

                typeof(Renderer)
                );
        }

        public static GameObject CloneWithSpecificComponents(GameObject original, params Type[] componentsToKeep)
        {
            // a disabled parent prevents Awake() from firing on the clone
            GameObject dummyParent = new GameObject("TempDisabledParent");
            dummyParent.SetActive(false);

            GameObject clone = UnityEngine.Object.Instantiate(original, dummyParent.transform, true);

            HashSet<Type> allowedTypes = new HashSet<Type>(componentsToKeep);
            allowedTypes.Add(typeof(Transform));

            Component[] allComponents = clone.GetComponentsInChildren<Component>(true);
            for (int i = allComponents.Length - 1; i >= 0; i--)
            {
                Component comp = allComponents[i];
                if (comp == null) continue;

                Type compType = comp.GetType();
                bool shouldKeep = false;

                foreach (Type allowedType in allowedTypes)
                {
                    if (allowedType.IsAssignableFrom(compType))
                    {
                        shouldKeep = true;
                        break;
                    }
                }

                if (!shouldKeep)
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                }
            }

            clone.transform.SetParent(original.transform.parent, true);

            UnityEngine.Object.Destroy(dummyParent);

            return clone;
        }
    }

}
