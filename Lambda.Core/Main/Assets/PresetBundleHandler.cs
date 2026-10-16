using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Networking;
using System;
using System.Collections.Generic;

namespace Lambda.Core.Main.AssetBundleHandling;

public class RuntimeBundleLoader : Singleton<RuntimeBundleLoader>, IDisposable
{
    public List<Item> ItemsToLoad { get; private set; }

    // Every template in ItemsToLoad, in insertion order. This is what goes over the wire.
    public List<string> TemplateIdsToLoad { get; private set; }

    private readonly HashSet<string> _cachedItems;

    // TODO: this has to be up before OnGameStarted (for late joiners)
    // this lifecycle needs to be improved for in between raids
    public RuntimeBundleLoader()
    {
        ItemsToLoad = [];
        TemplateIdsToLoad = [];
        _cachedItems = [];

        H.OnGameDispose += ResetCache;
    }

    public void Dispose()
    {
        H.OnGameDispose -= ResetCache;
        Release(this);
    }

    private void ResetCache()
    {
        ItemsToLoad.Clear();
        TemplateIdsToLoad.Clear();
        _cachedItems.Clear();
    }

    private bool TryCache(Item part)
    {
        if (!_cachedItems.Add(part.TemplateId)) return false;

        ItemsToLoad.Add(part);
        TemplateIdsToLoad.Add(part.TemplateId);
        return true;
    }

    // Returns the template ids that were not cached yet
    public List<string> AddToCacheAndGetDelta(IEnumerable<Item> items)
    {
        int start = TemplateIdsToLoad.Count;

        foreach (var item in items)
        {
            AddToCache(item);
        }

        return TemplateIdsToLoad.GetRange(start, TemplateIdsToLoad.Count - start);
    }

    public void AddToCache(List<Item> items)
    {
        foreach (var item in items)
        {
            AddToCache(item);
        }
    }

    public void AddToCache(Item item)
    {
        foreach (var part in item.GetAllItems())
        {
            TryCache(part);

            if (part is Weapon weapon && FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
            {
                AddToCache(ammo);
            }
        }
    }

    public void AddToCache(List<string> templateIds)
    {
        if (templateIds == null) return;

        foreach (string templateId in templateIds)
        {
            if (_cachedItems.Contains(templateId)) continue;

            if (IU.TryCreateItem(templateId, out Item item))
            {
                TryCache(item);
            }
        }
    }

    public async UniTask LoadEverythingInCache()
    {
        var prefabsToLoad = new HashSet<ResourceKey>();

        foreach (var item in ItemsToLoad)
        {
            foreach (var i in item.GetAllItems())
            {
                if (i.Template?.AllResources != null)
                {
                    prefabsToLoad.UnionWith(i.Template.AllResources);
                }
            }
        }

        if (prefabsToLoad.Count > 0)
        {
            await H.PoolManagerClass.LoadBundlesAndCreatePools(
                PoolManagerClass.PoolsCategory.Raid,
                PoolManagerClass.AssemblyType.Local,
                new List<ResourceKey>(prefabsToLoad),
                JobPriorityClass.Immediate,
                null,
                default
            );
        }
    }
}
