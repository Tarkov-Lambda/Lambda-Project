using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    public class TeamStatusPlayer : MonoBehaviour
    {
        [SerializeField] private Image iconPlayer;
        [SerializeField] private Image iconDead;

        [SerializeField] private float opacityWhenAlive;
        [SerializeField] private float opacityWhenDead;

        public void SetColor(Color color)
        {
            return; // looks bad
            iconPlayer.SetColorKeepGraphicAlpha(color);
            iconDead.SetColorKeepGraphicAlpha(color);
        }

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
