using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundData", menuName = "Audio/Create Audio Collection")]
public class LambdaSounds : ScriptableObject
{
    [Header("Noises")]
    public AudioClip[] LadderNoiseWood;
    public AudioClip[] LadderNoiseMetal;
    public AudioClip LandedOnFreefallReseter;
    public AudioClip[] HeadshotHelmet;
    public AudioClip[] HeadshotFlesh;

    [Header("Bomb SFX")]
    public AudioClip BombTick;
    public AudioClip Planting;
    public AudioClip Planted;
    public AudioClip Defusing;
    public AudioClip Defused;
    public AudioClip BeforeExploding;

    [Header("Molotov")]
    public AudioClip MolotovExplosion;
    public AudioClip MolotovBurning;
    public AudioClip MolotovBurnedOut;
    public AudioClip MolotovExtinquished;

    [Header("Smoke")]
    public AudioClip SmokeExplosion;
    public AudioClip SmokeSmoking;
    public AudioClip SmokeDissipating;

    [Header("Other")]
    public AudioClip[] CountdownTick;
}
