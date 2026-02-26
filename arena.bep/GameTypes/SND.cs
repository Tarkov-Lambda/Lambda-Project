using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifp.arena.bep.GameTypes
{
    internal class SND : BaseGameMode
    {
        public float RoundTime = 120f;

        public float BombTimer = 45f;
        public float DefuseTime = 6f;
        public int maxRounds = 9;

        override public void roundEnd(Faction faction = Faction.None)
        {
            base.roundEnd();
        }

    }


}
