using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace ifp.arena.bep.Core.UI
{
    internal class ImmutableItemsCache : Singleton<ImmutableItemsCache>, IDisposable
    {
        Dictionary<string, Item> cacheImmutableItems = new Dictionary<string, Item>();

        public Item GetImmutableItem(string bsgId)
        {
            if (cacheImmutableItems.ContainsKey(bsgId))
                return cacheImmutableItems[bsgId];

            var weaponBuild = PresetUtils.GetCustomTemplate(bsgId);
            if (weaponBuild != null)
                return weaponBuild.Item;

            Item newImmutableItem = ItemsUtils.ItemFactory.CreateItem(MongoID.Generate(), bsgId, null);
            cacheImmutableItems.Add(bsgId, newImmutableItem);
            return newImmutableItem;
        }

        public void Dispose()
        {
            cacheImmutableItems.Clear();
            Release(this);
        }
    }
}
