using Comfort.Common;
using Cysharp.Threading.Tasks;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    public class AudioHandler : Singleton<AudioHandler>, IDisposable
    {
        public AssetBundle audioBundle { get; private set; }
        public LambdaSounds prefabSounds { get; private set; }

        public AudioHandler()
        {
            audioBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "audio"));
            prefabSounds = audioBundle.LoadAsset<LambdaSounds>("Assets/Sounds/SoundData.asset");
        }

        public BetterSource PlayAtPoint(Vector3 pos, AudioClip clip, int rolloff = 10000, BetterAudio.AudioSourceGroupType overrideSourceGroup = BetterAudio.AudioSourceGroupType.Environment)
        {
            return Singleton<BetterAudio>.Instance.PlayAtPoint(
                pos,
                clip,
                distance: CameraClass.Instance.Distance(pos),
                sourceGroup: overrideSourceGroup,
                rolloff: rolloff,
                volume: 1f
            );
        }

        public void Dispose()
        {
            audioBundle.Unload(false);
            Release(this);
        }
    }
}
