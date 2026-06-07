using System.Collections.Generic;
using EFT.InventoryLogic;

// class full of bruteforce player manipulations 
public static class WeaponExtensions
{
    public static void TurnOffAllLights(this Weapon weapon)
    {
        IEnumerable<LightComponent> allLights = weapon.GetItemComponentsInChildren<LightComponent>();
        foreach (var lightComponent in allLights)
        {
            lightComponent.IsActive = false;
        }
    }

}