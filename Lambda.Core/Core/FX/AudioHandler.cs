using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.GameTypes;
using ifp.arena.shared.FX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Lambda.Core.Main.FX;

public class AudioHandler : Singleton<AudioHandler>, IDisposable
{
    private const string AUDIO_BUNDLE_NAME = "audio";
    private const string PREFAB_SOUNDS_PATH = "Assets/Sounds/SoundData.asset";
    private const string MUSIC_KIT_SOUNDS_PATH = "Assets/Sounds/MusicKitSoundData.asset";

    private static string AudioBundlePath => Path.Combine(Plugin.pathToBundles, AUDIO_BUNDLE_NAME);

    public AssetBundle AudioBundle { get; private set; }
    public LambdaSounds PrefabSounds { get; private set; }
    public MusicKit MusicKitSounds { get; private set; }

    public AudioHandler()
    {
        AudioBundle = AssetBundle.LoadFromFile(AudioBundlePath);
        PrefabSounds = AudioBundle.LoadAsset<LambdaSounds>(PREFAB_SOUNDS_PATH);
        MusicKitSounds = AudioBundle.LoadAsset<MusicKit>(MUSIC_KIT_SOUNDS_PATH);
    }

    public void Dispose()
    {
        AudioBundle.Unload(false);
        Release(this);
    }

    public BetterSource PlayAtPoint(Vector3 pos, AudioClip clip, int rolloff = 75, BetterAudio.AudioSourceGroupType overrideSourceGroup = BetterAudio.AudioSourceGroupType.Weaponry)
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
}
