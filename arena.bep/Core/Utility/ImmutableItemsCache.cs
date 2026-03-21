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

            var weaponBuild = FU.GetCustomTemplate(bsgId);
            if (weaponBuild != null)
                return weaponBuild.Item;

            IU.TryCreateItem(bsgId, out Item newImmutableItem);
            if (newImmutableItem != null)
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
