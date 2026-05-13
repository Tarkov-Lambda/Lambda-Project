
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using EFT;

namespace Lambda.Core.Main;

public static class ResourceKeyExtensions
{
    public static bool IsReadyNow(this ResourceKey key)
    {
        var pools = H.PoolManagerClass.method_0(PoolManagerClass.PoolsCategory.Raid);
        if (pools == null)
            return false;

        if (!pools.PoolsDictionary.TryGetValue(key, out var entry))
            return false;

        var task = entry.Source?.Task;
        return task != null &&
               task.IsCompleted &&
               !task.IsCanceled &&
               !task.IsFaulted;
    }

    public static async UniTask LoadBundles(this List<ResourceKey> resourceKeys)
    {
        await H.PoolManagerClass.LoadBundlesAndCreatePools(
            PoolManagerClass.PoolsCategory.Raid,
            PoolManagerClass.AssemblyType.Local,
            resourceKeys,
            JobPriorityClass.Immediate,
            null,
            default);
    }
}