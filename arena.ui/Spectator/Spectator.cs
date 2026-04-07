using ifp.arena.shared.Models;
using TMPro;
using UnityEngine;

namespace arena.ui
{
    public class Spectator : MonoBehaviour
    {
        [SerializeField] private TMP_Text textPlayerName;

        public void SetSpectatingPlayer(PlayerStats player)
        {
            textPlayerName.text = player.Name;
        }
    }
}
