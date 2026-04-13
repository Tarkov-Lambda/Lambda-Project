using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace ifp.arena.bep.Core.UI;

internal class PresetItemsCache : Singleton<PresetItemsCache>, IDisposable
{
    Dictionary<string, Item> cacheImmutableItems = new Dictionary<string, Item>();

    public PresetItemsCache()
    {
        H.OnGameStarted += ClearCache;
        H.OnGameDispose += ClearCache;
    }

    public Item GetPresetItem(string bsgId)
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

    private void ClearCache()
    {
        cacheImmutableItems.Clear();
    }

    public void ResetCachedItem(string bsgId)
    {
        cacheImmutableItems.Remove(bsgId);
    }

    public void Dispose()
    {
        H.OnGameStarted -= ClearCache;
        H.OnGameDispose -= ClearCache;
        ClearCache();
        Release(this);
    }
}