using Lambda.Shared.Models;
using TMPro;
using UnityEngine;

namespace Lambda.UI
{
    public class Spectator : MonoBehaviour
    {
        [SerializeField] private TMP_Text textPlayerName;

        public void SetSpectatingPlayer(PlayerScoreInfo player)
        {
            textPlayerName.text = player.Name;
        }
    }
}
