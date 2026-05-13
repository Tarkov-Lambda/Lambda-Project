using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemoryPack;

namespace Lambda.Shared.Models
{

    [MemoryPackable]
    public partial struct BuyCategory
    {
        public string name;
        public bool verticalLayout;
        public List<ShopItem> items;
    }

    [MemoryPackable]
    public partial struct ShopItem
    {
        // if the item is a weapon
        // we will be finding the actual build using PresetUtils
        public string bsgId;
        public string ammoId;
        public int price;
        public Faction faction; // Only shown for this faction
        public int maxQuantity; // Maximum amount on person (Grenades)
        public int maxBuy;      // Maximum round buy amount (Grenades)
        public int maxMagSize;  // Maximum mag allowed within this weapon (if the preset has a bigger mag = replace it on the server)
    }
}
