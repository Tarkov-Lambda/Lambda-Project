using Lambda.UI;
using Lambda.Core.Networking;
using System;
using Comfort.Common;

namespace Lambda.Core.Main.UI
{
    internal class KillFeedController : IDisposable
    {
        private readonly KillFeed killFeed;
        private readonly BSGItemInfoProvider itemInfoProvider;

        internal KillFeedController(KillFeed killFeed, BSGItemInfoProvider itemInfoProvider)
        {
            this.killFeed = killFeed;
            this.itemInfoProvider = itemInfoProvider;

            Singleton<PlayerKilledPacketWarden>.Instance.BeforePacketAppliedOptimistically += OnPlayerKill;
            Singleton<PlayerKilledPacketWarden>.Instance.BeforePacketApplied += OnPlayerKill;
        }

        private void OnPlayerKill(PlayerKilledPacket packet)
        {
            PlayerContext victim = H.GetPlayerContext(packet.Player);
            if (!victim.IsAlive) return;

            PlayerContext killer = H.GetPlayerContext(packet.killer);

            PlayerContext assist = H.GetPlayerContext(packet.assist);

            string leftName = BuildLeftName(killer, assist);
            string rightName = FormatPlayer(victim);

            Faction leftFaction = killer == null ? Faction.None : killer.Faction;
            Faction rightFaction = victim == null ? Faction.None : victim.Faction;

            var pop = killFeed.Pop(leftName, leftFaction, rightName, rightFaction, packet.IsHeadshot);

            itemInfoProvider.RequestIcon(packet.weaponId, onRendered: (weaponSprite) =>
            {
                pop.SetWeaponSprite(weaponSprite);
            });
        }

        private static string FormatPlayer(PlayerContext pContext)
        {
            if (pContext == null)
                return string.Empty;

            string name = pContext.player.Profile.Nickname;
            string clantagPrefix = pContext.ClanTag.IsNullOrEmpty() ? "" : $"[{pContext.ClanTag}] ";

            return pContext.player.IsYourPlayer ? $"<b>{clantagPrefix}{name}</b>" : name;
        }

        private static string BuildLeftName(PlayerContext killer, PlayerContext assist)
        {
            if (killer == null)
                return string.Empty;

            string result = FormatPlayer(killer);

            if (assist != null)
            {
                result += $" + {FormatPlayer(assist)}";
            }

            return result;
        }

        public void Dispose()
        {
            Singleton<PlayerKilledPacketWarden>.Instance.BeforePacketAppliedOptimistically -= OnPlayerKill;
            Singleton<PlayerKilledPacketWarden>.Instance.BeforePacketApplied -= OnPlayerKill;
        }
    }
}
