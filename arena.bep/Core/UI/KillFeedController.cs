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

            PlayerKilledPacketHandler.AfterPacketApplied += OnPlayerKill;
        }

        private void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            H.Scoreboard.TryGetValue(killPacket.killer.Id, out PlayerScore playerKiller);
            H.Scoreboard.TryGetValue(killPacket.victim.Id, out PlayerScore playerVictim);

            string leftName = playerKiller?.player.Profile.Nickname;
            string rightName = playerVictim?.player.Profile.Nickname;

            Faction leftFaction = playerKiller == null ? Faction.None : playerKiller.Faction;
            Faction rightFaction = playerVictim == null ? Faction.None : playerVictim.Faction;

            itemInfoProvider.RequestIcon(killPacket.weaponId, onRendered: (weaponSprite) =>
            {
                killFeed.Pop(
                    leftName, leftFaction,
                    rightName, rightFaction,
                    weaponSprite, killPacket.IsHeadshot);
            });
        }

        public void Dispose()
        {
            PlayerKilledPacketHandler.AfterPacketApplied -= OnPlayerKill;
        }
    }
}
