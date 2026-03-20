using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ifp.arena.shared.Models
{
    public struct BuyCategory
    {
        public string name;
        public bool wide;
        public List<ShopItem> items;
    }

    public struct ShopItem
    {
        // if the item is a weapon
        // we will be finding the actual build using PresetUtils
        public string bsgId;
        public string ammoId;
        public int price;
        public Faction faction; // Only shown for this faction
        public int maxQuantity; // Maximum amount on person (Grenades)
        public int maxBuy; // Maximum round buy amount (Grenades)
    }
}
