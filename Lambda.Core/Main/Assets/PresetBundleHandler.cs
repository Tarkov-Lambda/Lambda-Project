using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Lambda.Core.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lambda.Core.Main.AssetBundleHandling;

public class PresetBundleHandler : Singleton<PresetBundleHandler>, IDisposable
{
    public List<Item> ItemsToLoad { get; private set; }
    private readonly HashSet<string> _cachedItems;

    // TODO: this has to be up before OnGameStarted (for late joiners)
    // this lifecycle needs to be improved for in between raids
    public PresetBundleHandler()
    {
        ItemsToLoad = [];
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
        _cachedItems.Clear();
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
        var disassembledItem = item.GetAllItems();

        foreach (var part in disassembledItem)
        {
            if (_cachedItems.Add(part.TemplateId))
            {
                ItemsToLoad.Add(part);
            }
        }

        foreach (var subItem in item.GetAllItems())
        {
            if (subItem is Weapon weapon && FU.TryGetGunAmmo(weapon, out AmmoItemClass ammo))
            {
                AddToCache(ammo);
            }
        }
    }

    public async UniTask LoadEverythingInCache()
    {
        var prefabsToLoad = new List<ResourceKey>();

        foreach (var item in ItemsToLoad)
        {
            D.Log(item.LocalizedName());
            foreach (var i in item.GetAllItems())
            {
                if (i.Template == null)
                    continue;

                if (i.Template.AllResources != null)
                {
                    prefabsToLoad.AddRange(i.Template.AllResources);
                }
            }
        }

        if (prefabsToLoad.Count > 0)
        {
            var distinctPrefabs = prefabsToLoad.Distinct().ToList();

            await H.PoolManagerClass.LoadBundlesAndCreatePools(
                PoolManagerClass.PoolsCategory.Raid,
                PoolManagerClass.AssemblyType.Local,
                distinctPrefabs,
                JobPriorityClass.Immediate,
                null,
                default
            );
        }
    }
}
