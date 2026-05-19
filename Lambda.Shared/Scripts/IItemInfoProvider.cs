using System;
using UnityEngine;

namespace Lambda.Shared
{
    public interface IItemInfoProvider
    {
        string FullName(string bsgId);
        string ShortName(string bsgId);

        void RequestIcon(string bsgId, Action<Sprite> callback);
    }
}
