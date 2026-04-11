using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.AssetsManager;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

using CorpseRagdoll = RagdollClass;

namespace ifp.arena.bep.Core.Dying;

public class RagdollCreator : Singleton<RagdollCreator>, IDisposable
{
    readonly Dictionary<Player, FakeCorpse> regsitry = new Dictionary<Player, FakeCorpse>();

    public void Dispose()
    {
        foreach (var kvp in regsitry)
        {
            if (kvp.Value != null)
            {
                GameObject.Destroy(kvp.Value.gameObject);
            }
        }
        regsitry.Clear();
    }

    // I need to rename this
    public void OnPacket(Player player)
    {
        CreateRagdollFromPlayer(player);
    }

    public void CreateLocalPlayerRagdoll()
    {
        Player mainPlayer = H.GetMainPlayer();
        FakeCorpse fakeCorpse = CreateRagdollFromPlayer(mainPlayer);

        PlayerCameraController playerCameraController = mainPlayer.GetComponent<PlayerCameraController>();
        fakeCorpse.SetAttachedCamera(playerCameraController.Camera);
        playerCameraController.enabled = false;
        UniTask.Delay(4000).ContinueWith(() =>
        {
            playerCameraController.enabled = true;

            if (H.Arena.ActiveRules is SND_ModeRules)
            {
                H.SpectatorManager.SwitchSpectatePlayer();
            }

            if (fakeCorpse != null)
            {
                fakeCorpse.SetAttachedCamera(null);
            }
        });
    }


    private FakeCorpse CreateRagdollFromPlayer(Player player)
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

        List<PlayerRigidbodySleepHierarchy> rigidbodySleepHierarchy;
        try
        {
            rigidbodySleepHierarchy = PlayerPoolObject.CreatePlayerRigidbodySleepHierarchy(rigidbodySpawners);
        }
        catch (InvalidOperationException)
        {
            rigidbodySleepHierarchy = new List<PlayerRigidbodySleepHierarchy>();
        }

        playerClone.SetActive(false);

        FakeCorpse fakeCorpse = playerClone.AddComponent<FakeCorpse>();

        fakeCorpse.PreInitRagdollData(rigidbodySpawners, jointSpawners, rigidbodySleepHierarchy);
        fakeCorpse.SetOwnerPlayer(player);
        fakeCorpse.SetBones(playerClone.GetComponentInChildren<PlayerBones>());

        playerClone.SetActive(true);

        if (!player.IsYourPlayer)
        {
            PlayerBones cloneBones = playerClone.GetComponentInChildren<PlayerBones>();
            try
            {
                fakeCorpse.method_17(
                    player.ProfileId,
                    player.Inventory.Equipment as InventoryEquipment,
                    player.Profile.Customization,
                    reinitBody: false,
                    Singleton<GameWorld>.Instance,
                    player.Side,
                    player.Velocity,
                    cloneBones.Pelvis.Original,
                    ragdollEnabled: false,
                    new BindableStateClass<Item>(null),
                    foreStillCorpse: false
                );
            }
            catch (Exception ex)
            {
                // because we keep creating a corpse for the same player over and over
                // there is some kind of a non lethal error happening here
                // too bad!
            }
        }

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

        fakeCorpse.VocalizeDeath(player.Speaker.PlayerVoice);

        return fakeCorpse;
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

    public void ClearAllCorpses()
    {
        foreach (var kvp in regsitry)
        {
            if (kvp.Value != null)
            {
                GameObject.Destroy(kvp.Value.gameObject);
            }
        }
        regsitry.Clear();
    }
}