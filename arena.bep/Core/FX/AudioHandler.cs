using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.AssetBundleHandling;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ifp.arena.bep.Core.FX;

public class AudioHandler : Singleton<AudioHandler>, IDisposable
{
    public AssetBundle AudioBundle { get; private set; }
    public LambdaSounds PrefabSounds { get; private set; }
    public MusicKit MusicKitSounds { get; private set; }

    public AudioHandler()
    {
        AudioBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(MapAssetBundleHandler.pathToBundlesDir, "audio"));
        PrefabSounds = AudioBundle.LoadAsset<LambdaSounds>("Assets/Sounds/SoundData.asset");
        MusicKitSounds = AudioBundle.LoadAsset<MusicKit>("Assets/Sounds/MusicKitSoundData.asset");
    }

    public BetterSource PlayAtPoint(Vector3 pos, AudioClip clip, int rolloff = 75, BetterAudio.AudioSourceGroupType overrideSourceGroup = BetterAudio.AudioSourceGroupType.Environment)
    {
        return H.BetterAudio.PlayAtPoint(
            pos,
            clip,
            distance: CameraClass.Instance.Distance(pos),
            sourceGroup: overrideSourceGroup,
            rolloff: rolloff,
            volume: 1f
        );
    }

    public static void PlayEquipSound(Item item)
    {
        AudioClip clip = H.EFTGUISounds.GetItemClip(item.ItemSound, EInventorySoundType.drop);
        if (clip != null) H.EFTGUISounds.PlaySound(clip);
    }

    public void Dispose()
    {
        AudioBundle.Unload(false);
        Release(this);
    }
}
