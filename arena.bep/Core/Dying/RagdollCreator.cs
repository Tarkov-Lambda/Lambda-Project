using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Networking;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

using CorpseRagdoll = RagdollClass;

namespace ifp.arena.bep.Core.Dying
{
    public class RagdollCreator : IDisposable
    {
        Dictionary<Player, FakeCorpse> regsitry;

        public RagdollCreator()
        {
            Singleton<PlayerKilledPacketHandler>.Instance.OnPlayerKilled += CreateRagdollFromPlayer;

            regsitry = new Dictionary<Player, FakeCorpse>();
        }

        public void Dispose()
        {
            Singleton<PlayerKilledPacketHandler>.Instance.OnPlayerKilled -= CreateRagdollFromPlayer;

            foreach (var kvp in regsitry)
            {
                if (kvp.Value != null)
                {
                    GameObject.Destroy(kvp.Value.gameObject);
                }
            }
            regsitry.Clear();
        }

        private void CreateRagdollFromPlayer(Player player)
        {
            if ( player == null || player.Id == Singleton<GameWorld>.Instance.MainPlayer.Id) return;

            GameObject playerClone = CloneWithSpecificComponents(player.gameObject,
                typeof(PlayerBody),
                typeof(PlayerBones),
                typeof(BodyPartCollider),

                typeof(RigidbodySpawner),
                typeof(CharacterJointSpawner),

                typeof(Rigidbody),
                typeof(Collider),
                typeof(Joint),

                typeof(MeshFilter),
                typeof(Renderer)
                );

            playerClone.name = player.name + " (fake corpse)";
            foreach (var col in playerClone.GetComponentsInChildren<Collider>())
            {
                col.gameObject.layer = 23;
            }

            UnityEngine.Object.DestroyImmediate(playerClone.GetComponent<Rigidbody>());
            UnityEngine.Object.DestroyImmediate(playerClone.GetComponent<CapsuleCollider>());

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
            Vector3 velocity = player.Velocity;
            float maxDepenetrationVelocity = EFTHardSettings.Instance.CorpseMaxDepenetrationVelocity;
            CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.Discrete;
            MonoBehaviour owner = fakeCorpse;
            Func<bool, float, bool> checkCorpseIsStill = (sleeping, timePass) => { return sleeping || timePass >= 15f; };
            PlayerBody clonePlayerBody = playerClone.GetComponentInChildren<PlayerBody>();
            Func<bool> isVisibleTest = () => true;
            Action onRigidbodyStopped = fakeCorpse.OnRigidbodyStopped;
            bool keepRigidbody = false;
            bool putToSleep = true;

            new CorpseRagdoll(
                rigidbodySpawners,
                jointSpawners,
                rigidbodySleepHierarchy,
                velocity,
                maxDepenetrationVelocity,
                collisionDetectionMode,
                owner,
                checkCorpseIsStill,
                clonePlayerBody,
                isVisibleTest,
                onRigidbodyStopped,
                keepRigidbody,
                putToSleep
                );

            
            if (player.HandsController != null && player.HandsController.ControllerGameObject != null)
            {
                GameObject fakePhysicalItem = CloneWithSpecificComponents(player.HandsController.ControllerGameObject,
                    typeof(MeshFilter),
                    typeof(Renderer),

                    typeof(BoxCollider)
                    );

                fakePhysicalItem.name += " (fake physical item)";

                const float pointSize = 0.03f;
                foreach (var boxCol in fakePhysicalItem.GetComponentsInChildren<BoxCollider>())
                {
                    boxCol.size = new Vector3(pointSize, pointSize, pointSize);
                    boxCol.isTrigger = false;

                    if (boxCol.gameObject == fakePhysicalItem)
                        continue;

                    boxCol.enabled = true;
                    boxCol.gameObject.layer = 23;
                    boxCol.gameObject.transform.localScale = Vector3.one;
                }

                fakePhysicalItem.AddComponent<Rigidbody>();

                FakeDroppedItem fakeDroppedItem = fakePhysicalItem.AddComponent<FakeDroppedItem>();
                fakeDroppedItem.SetOriginalItem(player.HandsController.Item);

                fakeCorpse.SetItemInHands(fakeDroppedItem);
            }


            if (regsitry.TryGetValue(player, out var corpse))
            {
                if (corpse != null)
                {
                    GameObject.Destroy(corpse.gameObject);
                }
            }
            regsitry[player] = fakeCorpse;

            fakeCorpse.VocalizeDeath(player.Speaker.PlayerVoice, clonePlayerBody.PlayerBones.HeadCameraCollider.transform);
        }

        private static GameObject CloneWithSpecificComponents(GameObject original, params Type[] componentsToKeep)
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
