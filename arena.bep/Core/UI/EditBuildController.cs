using System;
using UnityEngine;
using arena.ui;
using EFT.UI;
using ifp.arena.bep.Patches.Tarkov.UI.WeaponBuilds;
using EFT.InventoryLogic;
using Comfort.Common;

namespace ifp.arena.bep.Core.UI
{
    internal class EditBuildController : IDisposable
    {
        EditBuildLambdaPanel panel;

        internal EditBuildController(CommonUI commonUI, AssetBundle uibundle)
        {
            GameObject prefabEditBuildPanel = uibundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/EditBuild/EditBuildLambdaPanel.prefab");
            panel = GameObject.Instantiate(prefabEditBuildPanel, commonUI.EditBuildScreen.transform.Find("ButtonsPanel")).GetComponent<EditBuildLambdaPanel>();

            Patch_EditBuildScreen_Show.OnPostfix += EditBuildScreen_Show;
            Patch_EditBuildScreen_UpdateItem.OnPostfix += EditBuildScreen_UpdateItem;

            panel.gameObject.SetActive(false);
        }

        private void EditBuildScreen_Show()
        {
            panel.gameObject.SetActive(false);
        }

        private void EditBuildScreen_UpdateItem(Item newItem)
        {
            if (newItem == null)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panel.gameObject.SetActive(true);


            bool isPreferredLambdaPreset = false; // uhhhhhh idk

            panel.SetEquipped(isPreferredLambdaPreset, () => SetPreferredLambdaPreset(newItem));
        }

        // when the user click the EQUIP AS PREFERRED button
        void SetPreferredLambdaPreset(Item item)
        {
            // todo
        }

        public void Dispose()
        {
            Patch_EditBuildScreen_Show.OnPostfix -= EditBuildScreen_Show;
            Patch_EditBuildScreen_UpdateItem.OnPostfix -= EditBuildScreen_UpdateItem;

            if (panel != null)
                GameObject.Destroy(panel.gameObject);
        }
    }
}
