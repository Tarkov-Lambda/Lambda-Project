using arena.ui;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;
using System;

namespace ifp.arena.bep.Core.UI
{
    internal class KillFeedController : IDisposable
    {
        private readonly ArenaMatchUI matchUI;
        private readonly BSGItemInfoProvider itemInfoProvider;

        internal KillFeedController(ArenaMatchUI matchUI, BSGItemInfoProvider itemInfoProvider)
        {
            this.matchUI = matchUI;
            this.itemInfoProvider = itemInfoProvider;

            EventBus.OnPlayerKill += OnPlayerKill;
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
                matchUI.KillFeed.Add(
                    leftName, leftFaction,
                    rightName, rightFaction,
                    weaponSprite, killPacket.IsHeadshot);
            });
        }

        public void Dispose()
        {
            EventBus.OnPlayerKill -= OnPlayerKill;
        }
    }
}
