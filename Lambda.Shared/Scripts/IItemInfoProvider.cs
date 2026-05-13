using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.Shared
{
    public interface IItemInfoProvider
    {
        string FullName(string bsgId);
        string ShortName(string bsgId);

        void RequestIcon(string bsgId, Action<Sprite> callback);
    }
}
