using System.Collections;
using Comfort.Common;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.networking.TimeSync;
using UnityEngine;

namespace ifp.arena.bep.Core.Audio
{
    public sealed class MusicEventRouter : MonoBehaviour
    {
        private Coroutine _roundTenSecJob;
        private Coroutine _bombTenSecJob;
        private bool _bombPlantedMusicPlayed;

        private void OnEnable()
        {
            EventBus.OnEnter += HandleStateEnter;
            EventBus.OnEnd += HandleStateEnd;
            EventBus.OnBombStateChange += HandleBombStateChange;
            EventBus.OnRoundActionEnd += HandleRoundActionEnd;
        }

        private void OnDisable()
        {
            EventBus.OnEnter -= HandleStateEnter;
            EventBus.OnEnd -= HandleStateEnd;
            EventBus.OnBombStateChange -= HandleBombStateChange;
            EventBus.OnRoundActionEnd -= HandleRoundActionEnd;

            StopRoundTenSecondCountdown();
            StopBombTenSecondCountdown();
        }

        private void HandleStateEnter(MatchState state)
        {
            switch (state)
            {
                case MatchState.RoundPrepare:
                    _bombPlantedMusicPlayed = false;
                    H.PlayMusic(MusicEvent.RoundStart);
                    break;

                case MatchState.RoundAction:
                    H.PlayMusic(MusicEvent.StartAction);
                    StartRoundTenSecondCountdown();
                    break;

                case MatchState.RoundPlanted:
                    if (!_bombPlantedMusicPlayed)
                        H.PlayMusic(MusicEvent.BombPlanted);
                    StartBombTenSecondCountdown();
                    break;

                case MatchState.SideSwap:
                    H.PlayMusic(MusicEvent.ChooseTeam);
                    break;

                case MatchState.MatchEnd:
                    H.PlayMusic(MusicEvent.EndMatch);
                    break;
            }
        }

        private void HandleStateEnd(MatchState state)
        {
            switch (state)
            {
                case MatchState.RoundAction:
                    StopRoundTenSecondCountdown();
                    break;
                case MatchState.RoundPlanted:
                    StopBombTenSecondCountdown();
                    break;
            }
        }

        private void HandleBombStateChange(BombState bombState)
        {
            // More immediate than waiting for RoundPlanted state replication.
            if (bombState == BombState.Planted)
            {
                _bombPlantedMusicPlayed = true;
                H.PlayMusic(MusicEvent.BombPlanted);
            }

            // If the objective is resolved early, ensure we don't play the 10-second cue late.
            if (bombState is BombState.Defused or BombState.Exploded)
            {
                StopBombTenSecondCountdown();
            }
        }

        private void HandleRoundActionEnd(RoundActionPhaseEnd payload)
        {
            if (H.MainPlayer == null) return;

            // MVP overrides win/lose.
            if (payload.mvpId == H.MainPlayer.Id)
            {
                H.PlayMusic(MusicEvent.MVP);
                return;
            }

            var myScore = H.GetPlayerScore(H.MainPlayer.Id);
            if (myScore != null && myScore.faction == payload.winner)
                H.PlayMusic(MusicEvent.WonRound);
            else
                H.PlayMusic(MusicEvent.LostRound);
        }

        private void StartRoundTenSecondCountdown()
        {
            StopRoundTenSecondCountdown();
            _roundTenSecJob = StartCoroutine(PlayTenSecondCue(MatchState.RoundAction, MusicEvent.RoundTenSecCount));
        }

        private void StartBombTenSecondCountdown()
        {
            StopBombTenSecondCountdown();
            _bombTenSecJob = StartCoroutine(PlayTenSecondCue(MatchState.RoundPlanted, MusicEvent.BombTenSecCount));
        }

        private void StopRoundTenSecondCountdown()
        {
            if (_roundTenSecJob != null)
            {
                StopCoroutine(_roundTenSecJob);
                _roundTenSecJob = null;
            }
        }

        private void StopBombTenSecondCountdown()
        {
            if (_bombTenSecJob != null)
            {
                StopCoroutine(_bombTenSecJob);
                _bombTenSecJob = null;
            }
        }

        private IEnumerator PlayTenSecondCue(MatchState expectedState, MusicEvent evt)
        {
            if (!Singleton<ArenaController>.Instantiated)
                yield break;

            // Use server-synced timestamps for better alignment than local timers.
            var arena = Singleton<ArenaController>.Instance;
            double targetServerTime = arena.ServerPhaseStartSeconds + arena.PhaseDurationSeconds - 10d;
            float delay = (float)(targetServerTime - NetworkTime.ServerNowSeconds);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Always play if we're still in the expected phase.
            if (Singleton<ArenaController>.Instantiated && H.Arena.session != null && H.Arena.session.roundState == expectedState)
                H.PlayMusic(evt);
        }
    }

}