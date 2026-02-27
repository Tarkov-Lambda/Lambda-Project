using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

using CorpseRagdoll = RagdollClass;

namespace ifp.arena.bep
{
    public class RagdollCreator : IDisposable
    {
        public RagdollCreator()
        {
            BaseGameMode.OnPlayerKilled += CreateRagdollFromPlayer;
        }

        public void Dispose()
        {
            BaseGameMode.OnPlayerKilled -= CreateRagdollFromPlayer;
        }

        public static void CreateRagdollFromPlayer(Player player)
        {
            GameObject playerClone = CloneWithSpecificComponents(player.gameObject,
                typeof(PlayerBody),
                typeof(PlayerBones),
                typeof(BodyPartCollider),

                typeof(RigidbodySpawner),
                typeof(CharacterJointSpawner),

                typeof(Rigidbody),
                typeof(Collider),
                typeof(Joint),

                typeof(Renderer)
                );

            playerClone.name = player.name + " (fake corpse)";
            foreach (var col in playerClone.GetComponentsInChildren<Collider>())
            {
                col.gameObject.layer = 23;
            }

            Component.DestroyImmediate(playerClone.GetComponent<Rigidbody>());
            Component.DestroyImmediate(playerClone.GetComponent<CapsuleCollider>());

            FakeCorpse fakeCorpse = playerClone.AddComponent<FakeCorpse>();

            RigidbodySpawner[] rigidbodySpawners = playerClone.GetComponentsInChildren<RigidbodySpawner>();
            foreach (var rbs in rigidbodySpawners)
            {
                AccessTools.Field(typeof(RigidbodySpawner), "rigidbody_0").SetValue(rbs, rbs.GetComponent<Rigidbody>());
            }

            CharacterJointSpawner[] jointSpawners = playerClone.GetComponentsInChildren<CharacterJointSpawner>();
            foreach (var js in jointSpawners)
            {
                AccessTools.Field(typeof(CharacterJointSpawner), "_joint").SetValue(js, js.GetComponent<Joint>());
            }

            List<PlayerRigidbodySleepHierarchy> rigidbodySleepHierarchy = PlayerPoolObject.CreatePlayerRigidbodySleepHierarchy(rigidbodySpawners);
            Vector3 velocity = Vector3.zero;
            float maxDepenetrationVelocity = EFTHardSettings.Instance.CorpseMaxDepenetrationVelocity;
            CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.Discrete;
            MonoBehaviour owner = fakeCorpse;
            Func<bool, float, bool> checkCorpseIsStill = (bool sleeping, float timePass) => { return sleeping || timePass >= 15f; };
            PlayerBody playerBody = playerClone.GetComponentInChildren<PlayerBody>();
            Func<bool> isVisibleTest = () => true;
            Action onRigidbodyStopped = fakeCorpse.OnRigidbodyStopped;
            bool keepRigidbody = false;
            bool putToSleep = false;

            new CorpseRagdoll(
                rigidbodySpawners,
                jointSpawners,
                rigidbodySleepHierarchy,
                velocity,
                maxDepenetrationVelocity,
                collisionDetectionMode,
                owner,
                checkCorpseIsStill,
                playerBody,
                isVisibleTest,
                onRigidbodyStopped,
                keepRigidbody,
                putToSleep
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
