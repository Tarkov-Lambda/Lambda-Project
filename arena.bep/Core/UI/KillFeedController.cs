using arena.ui;
using arena.ui.killfeed;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using System;

namespace ifp.arena.bep.Core.UI
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
            PlayerScore victimScore = H.GetPlayerScore(packet.victim);
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
