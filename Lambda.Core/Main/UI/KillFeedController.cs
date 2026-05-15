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

            Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied += OnPlayerKill;
        }

        private void OnPlayerKill(PlayerKilledPacket packet)
        {
            PlayerScore victim = H.GetPlayerScore(packet.Player);
            if (!victim.IsAlive) return;

            PlayerScore killer = H.GetPlayerScore(packet.killer);

            PlayerScore assist = H.GetPlayerScore(packet.assist);

            string leftName = BuildLeftName(killer, assist);
            string rightName = FormatPlayer(victim);

            Faction leftFaction = killer == null ? Faction.None : killer.Faction;
            Faction rightFaction = victim == null ? Faction.None : victim.Faction;

            var pop =
                killFeed.Pop(
                    leftName, leftFaction,
                    rightName, rightFaction,
                    packet.IsHeadshot);

            itemInfoProvider.RequestIcon(packet.weaponId, onRendered: (weaponSprite) =>
            {
                pop.SetWeaponSprite(weaponSprite);
            });
        }

        private static string FormatPlayer(PlayerScore player)
        {
            if (player == null)
                return string.Empty;

            string name = player.player.Profile.Nickname;

            return player.player.IsYourPlayer ? $"<b>{name}</b>" : name;
        }

        private static string BuildLeftName(PlayerScore killer, PlayerScore assist)
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
            Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied -= OnPlayerKill;
        }
    }
}
