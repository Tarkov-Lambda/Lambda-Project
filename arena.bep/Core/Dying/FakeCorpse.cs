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
        Collider[] cols;

        FakeDroppedItem itemInHands;

        BetterSource speaker;

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

        public void VocalizeDeath(string playerVoiceId, Transform head)
        {
            EPhraseTrigger trigger = EPhraseTrigger.OnDeath;
            ETagStatus tags = ETagStatus.BadlyInjured;

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
