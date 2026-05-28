using Comfort.Common;
using EFT;
using Lambda.Core;
using Lambda.Core.Networking;
using Lambda.Core.Networking.Commands;
using PacketWarden;
using UnityEngine;

public static class GeneralPlayerCommands
{
    [ChatCommand("login", "Requests server login", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Login(CommandContext ctx, string password)
    {
        LambdaPlugin.Password.Value = password;
        Singleton<AdminLoginPacketWarden>.Instance.Send();
    }

    [ChatCommand("pause", "Requests server login", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Pause(CommandContext ctx)
    {
        Singleton<SessionPausePacketWarden>.Instance.Pause();
    }

    [ChatCommand("unpause", "Requests server login", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Unpause(CommandContext ctx)
    {
        Singleton<SessionPausePacketWarden>.Instance.Unpause();
    }

    [ChatCommand("setclan", "Set your new clan tag", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void SetClanTag(CommandContext ctx, string clanTag)
    {
        Singleton<ClanTagResyncPacketWarden>.Instance.Send(clanTag);
    }

    [ChatCommand("volume_music", "Set music volume", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void SetVolumeMusic(CommandContext ctx, float volume)
    {
        LambdaPlugin.MusicVolume.Value = Mathf.Clamp01(volume);
    }

    [ChatCommand("suicide", "Commit suicide", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void Suicide(CommandContext ctx)
    {
        var damageInfo = new DamageInfoStruct
        {
            Damage = 1f,
            BodyPartColliderType = EBodyPartColliderType.RibcageUp
        };
        Singleton<PlayerKilledPacketWarden>.Instance.Send(damageInfo, H.MainPlayer, H.MainPlayer);
    }

    [ChatCommand("resetme", "Safely reset yourself", CommandTarget.ClientOnly, PacketAuthority.Anyone)]
    public static void ResetMe(CommandContext ctx)
    {
        PU.OpenEyes();
        H.BetterAudio.FadeMixerVolume(H.BetterAudio.AudioMixerData.InGameVolumeMixer, 0f, 1f);
        Singleton<EquipmentResyncPacketWarden>.Instance.Send(H.MainPlayer);
        H.MainPlayer.GetComponent<EftGamePlayerOwner>().Mute(false);
    }
}