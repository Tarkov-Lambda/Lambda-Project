using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lambda.Core.Patches;

internal abstract class PatchGroup
{
    private readonly Stack<ModulePatch> _enabledPatches = new();

    public void Enable()
    {
        Disable();

        var nestedPatchTypes = GetType()
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
            .Where(t => t.IsSubclassOf(typeof(ModulePatch)) && !t.IsAbstract);

        foreach (var type in nestedPatchTypes)
        {
            var patch = (ModulePatch)Activator.CreateInstance(type);
            _enabledPatches.Push(patch);
            patch.Enable();
        }
    }

    public void Disable()
    {
        while (_enabledPatches.Count > 0)
            _enabledPatches.Pop().Disable();
    }
}
