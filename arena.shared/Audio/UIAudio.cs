using System;
using UnityEngine;

namespace ifp.arena.shared
{
    [CreateAssetMenu(fileName = "UIAudio", menuName = "Audio/UI Audio")]
    public class UIAudio : ScriptableObject
    {
        public AudioClip BuyWeapon;
        public AudioClip BuyArmor;
        public AudioClip BuyGrenade;

        public AudioClip timerTick;
    }
}
