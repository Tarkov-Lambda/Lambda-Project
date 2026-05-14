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
    public List<Item> itemsToLoad { get; private set; }

    public PresetBundleHandler()
    {
        itemsToLoad = [];

        H.OnGameStarted += Initialize;
        H.OnGameDispose += ResetCache;

        if (H.IsInRaid()) Initialize();
    }

    public void Dispose()
    {
        H.OnGameStarted -= Initialize;
        H.OnGameDispose -= ResetCache;
        Release(this);
    }

    private void ResetCache(SessionStartPacket packet) => ResetCache();

    private void ResetCache()
    {
        itemsToLoad.Clear();
    }

    public void Initialize()
    {

    }

    public void AddToCache(List<Item> items)
    {
        foreach (var item in items)
        {
            AddToCache(item);
        }
    }

    // add unique items if the template id is unique
    // used for bundle loading during SessionStart
    public void AddToCache(Item item)
    {
        var disassembledItem = item.GetAllItems();

        foreach (var part in disassembledItem)
        {
            var foundItem = itemsToLoad.FirstOrDefault(itemToLoad => itemToLoad.TemplateId == part.TemplateId);

            if (foundItem == null) itemsToLoad.Add(part);
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

        foreach (var item in itemsToLoad)
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
