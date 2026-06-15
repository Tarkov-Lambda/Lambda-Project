using EFT;
using HarmonyLib;

public static class Accessors
{
    public static readonly AccessTools.FieldRef<Player.FirearmController, Player> FirearmControllerPlayerRef = AccessTools.FieldRefAccess<Player.FirearmController, Player>("_player");
}