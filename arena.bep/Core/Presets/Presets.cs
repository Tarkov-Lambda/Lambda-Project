using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using ifp.arena.bep.Core;
using Newtonsoft.Json;

namespace ifp.arena.Core
{
    public static class ItemSpawner
    {
        

        private static bool TryCreateItem(string templateId, out Item newItem)
        {
            newItem = null;

            if (!Singleton<ItemFactoryClass>.Instantiated)
                return false;

            if (!Singleton<ItemFactoryClass>.Instance.ItemTemplates.ContainsKey(templateId))
                return false;

            newItem = Singleton<ItemFactoryClass>.Instance.CreateItem(MongoID.Generate(), templateId, itemDiff: null);

            return newItem != null;
        }
    }
}