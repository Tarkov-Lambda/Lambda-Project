using arena.ui;
using ifp.arena.bep.Core.Gamemode;
using System;

namespace ifp.arena.bep.Core.UI
{
    internal class MatchResultController : IDisposable
    {
        readonly ArenaMatchUI matchUI;

        internal MatchResultController(ArenaMatchUI matchUI)
        {
            this.matchUI = matchUI;

            EventBus.OnRoundActionEnd += OnRoundActionEnd;
        }

        void OnRoundActionEnd(RoundActionPhaseEnd data)
        {
            bool win = data.winner == H.MainPlayerScore.Faction;
            string mainTitle = win ? "ROUND WON" : "ROUND LOST";

            string subTitle = "";

            if (H.GetPlayer(data.mvpId) != null && data.mvpReason != null)
            {
                subTitle = $"{H.GetPlayer(data.mvpId).Profile.Nickname} awarded for {data.mvpReason}";
            }

            matchUI.PopupMatchEnd.Pop(win, mainTitle, subTitle);
        }

        public void Dispose()
        {
            EventBus.OnRoundActionEnd -= OnRoundActionEnd;
        }
    }
}
