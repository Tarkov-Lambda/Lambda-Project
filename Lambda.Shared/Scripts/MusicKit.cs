using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MusicKitSoundData", menuName = "Audio/Create Music Kit Collection")]
public class MusicKit : ScriptableObject
{
    [Header("Music")]
    public AudioClip[] RoundPrepare;
    public AudioClip BombPlanted45;
    public AudioClip Round10Seconds;
    public AudioClip RoundWon;
    public AudioClip RoundLost;
}
