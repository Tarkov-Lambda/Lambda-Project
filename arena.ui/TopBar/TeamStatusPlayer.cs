using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class TeamStatusPlayer : MonoBehaviour
    {
        [SerializeField] private Image iconPlayer;
        [SerializeField] private Image iconDead;

        [SerializeField] private float opacityWhenAlive;
        [SerializeField] private float opacityWhenDead;

        public void SetAlive()
        {
            iconPlayer.SetAlpha(opacityWhenAlive);
            iconDead.enabled = false;
        }

        public void SetDead()
        {
            iconPlayer.SetAlpha(opacityWhenDead);
            iconDead.enabled = true;
        }
    }
}
