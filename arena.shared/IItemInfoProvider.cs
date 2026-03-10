using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ifp.arena.shared
{
    public interface IItemInfoProvider
    {
        string FullName(string bsgId);
        string ShortName(string bsgId);

        Sprite Icon(string bsgId);
    }
}
