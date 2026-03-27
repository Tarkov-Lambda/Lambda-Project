using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using RootMotion.FinalIK;
using System.Collections.Generic;
using UnityEngine;

using EasyAssetsExtensions = GClass1857;

namespace ifp.arena.bep.Core.Dying
{
    public class FakeCorpse : Corpse
    {
        PlayerBones bones;

        public Player OwnerPlayer { get; private set; }

        FakeDroppedItem itemInHands;

        BetterSource speaker;

        Camera attachedCamera;
        Vector3 attacthedCameraLocalPos;
        Quaternion attacthedCameraLocalRot;

        RigidbodySpawner[] _preInitRbSpawners;
        CharacterJointSpawner[] _preInitJointSpawners;
        List<PlayerRigidbodySleepHierarchy> _preInitSleepHierarchy;

        public void PreInitRagdollData(
            RigidbodySpawner[] rigidbodySpawners,
            CharacterJointSpawner[] jointSpawners,
            List<PlayerRigidbodySleepHierarchy> sleepHierarchy)
        {
            _preInitRbSpawners = rigidbodySpawners;
            _preInitJointSpawners = jointSpawners;
            _preInitSleepHierarchy = sleepHierarchy;
        }

        public new void Awake()
        {
            AccessTools.Field(typeof(Corpse), "rigidbodySpawner_0").SetValue(this, _preInitRbSpawners);
            AccessTools.Field(typeof(Corpse), "characterJointSpawner_0").SetValue(this, _preInitJointSpawners);
            AccessTools.Field(typeof(Corpse), "list_0").SetValue(this, _preInitSleepHierarchy);
        }

        public void SetOwnerPlayer(Player player)
        {
            OwnerPlayer = player;
        }

        public void SetItemInHands(FakeDroppedItem item)
        {
            itemInHands = item;
        }

        public void SetBones(PlayerBones bones)
        {
            this.bones = bones;
        }

        public void VocalizeDeath(string playerVoiceId)
        {
            EPhraseTrigger trigger = EPhraseTrigger.OnDeath;
            ETagStatus tags = ETagStatus.BadlyInjured;

            Transform head = bones.HeadCameraCollider.transform;

            string key = ResourceKeyManagerAbstractClass.TakePhrasePath(playerVoiceId);
            if (!EasyAssetsExtensions.TryGetAsset<Voice>(Singleton<IEasyAssets>.Instance, out var asset, key))
            {
                return;
            }

            TagBank tagBank = null;
            TagBank[] banks = asset.Banks;
            foreach (TagBank tagBank2 in banks)
            {
                if (tagBank2.Trigger == trigger)
                {
                    tagBank = tagBank2;
                    break;
                }
            }

            if (tagBank == null)
            {
                return;
            }

            int? importance = tagBank.Importance;
            tags |= ETagStatus.Solo;
            TaggedClip taggedClip = tagBank.Match((int)tags);
            if (taggedClip == null)
            {
                return;
            }

            BetterSource speaker = Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Character, true);
            speaker.StartTrackingPosition(head);
            speaker.SetMixerGroup(MonoBehaviourSingleton<BetterAudio>.Instance.ObservedPlayerSpeechMixer);
            speaker.Play(taggedClip.Clip, null, 1f);
        }

        public void SetAttachedCamera(Camera cam)
        {
            attachedCamera = cam;
        }

        void Update()
        {
            if (attachedCamera != null)
            {
                Matrix4x4 parentMatrix = bones.HeadCameraCollider.transform.localToWorldMatrix;
                Matrix4x4 localMatrix = Matrix4x4.TRS(attacthedCameraLocalPos, attacthedCameraLocalRot, Vector3.one);
                Matrix4x4 worldMatrix = parentMatrix * localMatrix;
                attachedCamera.transform.position = worldMatrix.GetColumn(3);
                attachedCamera.transform.rotation = worldMatrix.rotation;
            }
        }

        public void OnRigidbodyStopped()
        {
        }

        void OnDestroy()
        {
            if (itemInHands != null)
                Destroy(itemInHands.gameObject);

            if (speaker != null)
                speaker.Release();
        }
    }
}
