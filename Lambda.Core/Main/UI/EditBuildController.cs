using System;
using System.Reflection;
using UnityEngine;
using EFT.UI;
using Lambda.Core.Patches.Tarkov.UI.WeaponBuilds;
using EFT.InventoryLogic;
using Comfort.Common;
using Lambda.UI;
// Assuming you have access to your WeaponBuildClass namespace

namespace Lambda.Core.Main.UI
{
    internal class EditBuildController : IDisposable
    {
        FieldInfo _weaponBuildClassField = typeof(EditBuildScreen).GetField("weaponBuildClass", BindingFlags.NonPublic | BindingFlags.Instance);

        EditBuildLambdaPanel panel;
        EditBuildScreen editBuildScreen;

        readonly CommonUI commonUI;
        readonly AssetBundle uibundle;

        readonly GameObject prefabEditBuildPanel;


        internal EditBuildController(CommonUI commonUI, AssetBundle uibundle)
        {
            this.commonUI = commonUI;
            this.uibundle = uibundle;

            prefabEditBuildPanel = uibundle.LoadAsset<GameObject>("Packages/com.lambda.editor/Lambda.UI/EditBuild/EditBuildLambdaPanel.prefab");

            Initialize();
        }

        private void Initialize()
        {
            panel = GameObject.Instantiate(prefabEditBuildPanel, commonUI.EditBuildScreen.transform.Find("ButtonsPanel")).GetComponent<EditBuildLambdaPanel>();

            editBuildScreen = commonUI.EditBuildScreen;
            Patch_EditBuildScreen_Show.OnPostfix += EditBuildScreen_Show;
            Patch_EditBuildScreen_UpdateItem.OnPostfix += EditBuildScreen_UpdateItem;

            panel.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            H.AfterApplicationLoaded -= Initialize;

            Patch_EditBuildScreen_Show.OnPostfix -= EditBuildScreen_Show;
            Patch_EditBuildScreen_UpdateItem.OnPostfix -= EditBuildScreen_UpdateItem;

            if (panel != null)
                GameObject.Destroy(panel.gameObject);
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

            WeaponBuildClass currentBuild = _weaponBuildClassField.GetValue(editBuildScreen) as WeaponBuildClass;

            bool isPreferredLambdaPreset = false;

            if (WeaponPresetManager.Instantiated && currentBuild != null)
            {
                if (WeaponPresetManager.Instance.SelectedGunPresetMap.TryGetValue(newItem.TemplateId, out string preferredMongoId))
                {
                    isPreferredLambdaPreset = currentBuild.Id.ToString() == preferredMongoId;
                }
            }

            panel.SetEquipped(isPreferredLambdaPreset, () => SetPreferredLambdaPreset(newItem, currentBuild));
        }

        void SetPreferredLambdaPreset(Item item, WeaponBuildClass build)
        {
            if (build == null)
            {
                D.Notify("The build must be saved first.");
                return;
            }

            WeaponPresetManager.Instance.UpdateSelectedPreset(item.TemplateId, build.Id.ToString());

            panel.SetEquipped(true, () => SetPreferredLambdaPreset(item, build));
        }


    }
}