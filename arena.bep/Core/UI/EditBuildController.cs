using System;
using UnityEngine;
using arena.ui;
using EFT.UI;
using ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds;

namespace ifp.arena.bep.Core.UI
{
    internal class EditBuildController : IDisposable
    {
        EditBuildLambdaPanel panel;

        internal EditBuildController(CommonUI commonUI, AssetBundle uibundle)
        {

            GameObject prefabEditBuildPanel = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/EditBuild/EditBuildLambdaPanel.prefab");
            panel = GameObject.Instantiate(prefabEditBuildPanel, commonUI.EditBuildScreen.transform.Find("ButtonsPanel")).GetComponent<EditBuildLambdaPanel>();

            Patch_EditBuildScreen_Show.OnShow += EditBuildScreen_OnShow;
        }

        private void EditBuildScreen_OnShow()
        {
            panel.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            Patch_EditBuildScreen_Show.OnShow -= EditBuildScreen_OnShow;

            if (panel != null)
                GameObject.Destroy(panel.gameObject);
        }
    }
}
