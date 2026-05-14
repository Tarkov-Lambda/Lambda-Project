using Lambda.Shared;
using UnityEngine;

namespace Lambda.UI
{
    [CreateAssetMenu(fileName = "FactionColors", menuName = "Arena/Faction Colors")]
    public class FactionColors : ScriptableObject
    {
        [SerializeField] private Color ct = new Color(0.2f, 0.4f, 1f);
        [SerializeField] private Color t = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color spectator = Color.gray;
        [SerializeField] private Color none = Color.gray;

        public Color Get(Faction faction)
        {
            switch (faction)
            {
                case Faction.CT: return ct;
                case Faction.T: return t;
                case Faction.Spectator: return spectator;
                default: return none;
            }
        }

        public string GetHtmlString(Faction faction) => ColorUtility.ToHtmlStringRGB(Get(faction));
    }
}