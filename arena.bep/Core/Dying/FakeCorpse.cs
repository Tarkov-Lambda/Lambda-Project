using Comfort.Common;
using EFT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using EasyAssetsExtensions = GClass1857;
using EnumExtensions = GClass867;

namespace ifp.arena.bep.Core.Dying
{
    public class FakeCorpse : MonoBehaviour
    {
        PlayerBones bones;

        Collider[] cols;

        FakeDroppedItem itemInHands;

        BetterSource speaker;

        Camera attachedCamera;
        Vector3 attacthedCameraLocalPos;
        Quaternion attacthedCameraLocalRot;

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

        public void SetItemInHands(FakeDroppedItem item)
        {
            itemInHands = item;
        }

        public void SetBones(PlayerBones bones)
        {
            Plugin.Logger.LogInfo(bones.name);
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
