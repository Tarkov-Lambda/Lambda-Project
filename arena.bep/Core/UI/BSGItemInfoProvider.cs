using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using ifp.arena.shared;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using ItemIcon = GClass929;

namespace ifp.arena.bep.Core.UI
{
    internal class BSGItemInfoProvider : IItemInfoProvider, IDisposable
    {
        Dictionary<string, Item> cacheImmutableItems = new Dictionary<string, Item>();

        public string FullName(string bsgId)
        {
            if (string.IsNullOrEmpty(bsgId))
                return "empty id";
            try
            {
                MongoID mongoId = new MongoID(bsgId);
                return mongoId.LocalizedName();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogInfo(ex);
            }

            return "epop";
        }

        public string ShortName(string bsgId)
        {
            if (string.IsNullOrEmpty(bsgId))
                return "empty id";

            try
            {
                MongoID mongoId = new MongoID(bsgId);
                return mongoId.LocalizedShortName();
            }
            catch (Exception ex){
                Plugin.Logger.LogInfo(ex);
            }

            return "epop";
        }

        public void RequestIcon(string bsgId, Action<Sprite> onRendered)
        {
            Item immutableItem = GetImmutableItem(bsgId);

            ItemIcon itemIcon = ItemViewFactory.LoadItemIcon(immutableItem);
            if (itemIcon.Sprite != null)
            {
                onRendered?.Invoke(itemIcon.Sprite);
                return;
            }
            
            itemIcon.Changed.Bind(() => onRendered?.Invoke(itemIcon.Sprite));
        }

        private Item GetImmutableItem(string bsgId)
        {
            if (cacheImmutableItems.ContainsKey(bsgId))
                return cacheImmutableItems[bsgId];

            var weaponBuild = PresetUtils.GetCustomTemplate(bsgId);
            if (weaponBuild != null)
                return weaponBuild.Item;

            Item newImmutableItem = Singleton<ItemFactoryClass>.Instance.CreateItem(MongoID.Generate(), bsgId, null);
            cacheImmutableItems.Add(bsgId, newImmutableItem);
            return newImmutableItem;
        }

        public void Dispose()
        {
            foreach (var item in cacheImmutableItems)
            {
                // ???
            }

            cacheImmutableItems.Clear();
        }
    }
}
