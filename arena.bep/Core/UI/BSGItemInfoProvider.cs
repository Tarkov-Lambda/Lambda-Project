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
    internal class BSGItemInfoProvider : IItemInfoProvider
    {
        Sprite emptySprite;

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
            catch (Exception ex)
            {
                Plugin.Logger.LogInfo(ex);
            }

            return "epop";
        }

        public void RequestIcon(string bsgId, Action<Sprite> onRendered)
        {
            if (string.IsNullOrEmpty(bsgId))
            {
                if (emptySprite == null)
                    emptySprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 1, 1), Vector2.zero, 100);
                onRendered?.Invoke(emptySprite);
                return;
            }

            Item immutableItem = Singleton<ImmutableItemsCache>.Instance.GetImmutableItem(bsgId);

            if (immutableItem == null)
            {
                Plugin.Logger.LogWarning($"error creating immutable item for template '{bsgId}'");
                if (emptySprite == null)
                    emptySprite = Sprite.Create(Texture2D.blackTexture, new Rect(0, 0, 1, 1), Vector2.zero, 100);
                onRendered?.Invoke(emptySprite);
                return;
            }

            ItemIcon itemIcon = ItemViewFactory.LoadItemIcon(immutableItem);
            if (itemIcon.Sprite != null)
            {
                onRendered?.Invoke(itemIcon.Sprite);
                return;
            }

            itemIcon.Changed.Bind(() => onRendered?.Invoke(itemIcon.Sprite));
        }
    }
}
