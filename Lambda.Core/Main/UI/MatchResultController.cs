using arena.ui;
using Lambda.Core.Main.Gamemode;
using System;

namespace Lambda.Core.Main.UI
{
    internal class MatchResultController : IDisposable
    {
        readonly PopupMatchEnd popupMatchEnd;

        internal MatchResultController(PopupMatchEnd popupMatchEnd)
        {
            this.popupMatchEnd = popupMatchEnd;

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

            popupMatchEnd.Pop(win, mainTitle, subTitle);
        }

        public void Dispose()
        {
            EventBus.OnRoundActionEnd -= OnRoundActionEnd;
        }
    }
}
