using Comfort.Common;
using ifp.arena.bep.Core.AssetBundleHandling;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.FX
{
    internal class FXHandler : Singleton<FXHandler>, IDisposable
    {
        AssetBundle fxbundle;

        GameObject prefabFire;

        public FXHandler()
        {
            fxbundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(AssetBundleHandler.pathToBundlesDir, "fx"));
            prefabFire = fxbundle.LoadAsset<GameObject>("Assets/FX/FLAMES/Fire_zone_Animaton.prefab");
        }

        public void Dispose()
        {
            fxbundle.Unload(false);
        }
    }
}
