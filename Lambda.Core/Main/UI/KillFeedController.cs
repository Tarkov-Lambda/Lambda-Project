using arena.ui;
using arena.ui.killfeed;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using System;

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

            PlayerKilledPacketHandler.BeforePacketApplied += OnPlayerKill;
        }

        private void OnPlayerKill(PlayerKilledPacket packet)
        {
            PlayerScore victimScore = H.GetPlayerScore(packet.Player);
            if (!victimScore.IsAlive) return;

            PlayerScore killerScore = H.GetPlayerScore(packet.killer);

            string leftName = killerScore?.player.Profile.Nickname;
            string rightName = victimScore?.player.Profile.Nickname;

            Faction leftFaction = killerScore == null ? Faction.None : killerScore.Faction;
            Faction rightFaction = victimScore == null ? Faction.None : victimScore.Faction;

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

        public void Dispose()
        {
            PlayerKilledPacketHandler.BeforePacketApplied -= OnPlayerKill;
        }
    }
}
