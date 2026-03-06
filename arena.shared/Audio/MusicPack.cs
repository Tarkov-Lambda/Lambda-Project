using UnityEngine;

namespace ifp.arena.shared
{
    [CreateAssetMenu(fileName = "NewMusicPack", menuName = "Audio/Music Pack")]
    public class MusicPack : ScriptableObject
    {
        [Header("Pack Info")]
        public string packName;

        [Header("Main Menu")]
        public AudioClip[] mainMenu;

        [Header("Round Events")]
        public AudioClip[] roundStart;
        public AudioClip[] roundWin;
        public AudioClip[] roundLose;

        [Header("Bomb Events")]
        public AudioClip[] bombPlanted;
        public AudioClip[] bombTenSeconds;
        public AudioClip[] bombDefused;
        public AudioClip[] bombExploded;

        [Header("Player Events")]
        public AudioClip[] mvp;
        public AudioClip[] deathCam;

        [Header("Misc")]
        public AudioClip[] roundStartShort;
        public AudioClip[] actionCue;
    }
}
