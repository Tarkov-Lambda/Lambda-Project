using EFT;
using ifp.arena.shared;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Linq;
using Lambda.Core.Main.Gamemode;
using Comfort.Common;
using Lambda.Core.Networking;
using UnityEngine;
using Lambda.Core.Main.FX;
using Lambda.Core.Main;
using Cysharp.Threading.Tasks;

internal static class CustomInteractions
{
    public static bool TryHandleInteraction(GamePlayerOwner owner, IInteractive interactive, ref ActionsReturnClass result)
    {
        if (interactive == null) return true;

        Player player = owner.Player;
        if (player == null) return true;

        var roundState = H.Session.matchState;
        if (roundState != MatchState.RoundAction && roundState != MatchState.RoundPlanted)
            return true;

        var actions = new AvailableInteractionState();

        if (interactive is ObservedLootItem loot && H.Gamemode is SNDGamemode snd)
        {
            if (loot.TemplateId == Hardcode.BOMB_BACKPACK && H.MainPlayerScore.Faction != Faction.T)
            {
                result = actions;
                return false;
            }
        }

        if (interactive is Corpse corpse)
        {
            if (corpse.PlayerProfileID == H.MainPlayer.ProfileId)
            {
                result = actions;
                return false;
            }
        }

        if (interactive is Bombasik)
        {
            if (roundState != MatchState.RoundPlanted) return true;
            if (H.MainPlayer.IsInPronePose) return true;

            bool hasKit = TryFindItem(Hardcode.DEFUSE_KIT, out Item defuseKit);
            float time = hasKit ? SNDGamemode.defusingTime / 2 : SNDGamemode.defusingTime;

            actions.Actions.Add(new ActionsTypesClass
            {
                Name = "DEFUSE",
                Action = () => HandleDefuse(owner, player, time)
            });

            result = actions;
            return false;
        }

        if (interactive is BombPlantZone)
        {
            if (roundState != MatchState.RoundAction) return true;
            if (!TryFindItem(Hardcode.BOMB_BACKPACK, out Item bomb)) return true;

            float time = SNDGamemode.platingTime;

            actions.Actions.Add(new ActionsTypesClass
            {
                Name = "PLANT",
                Action = () => HandlePlant(owner, player, time)
            });

            result = actions;
            return false;
        }

        return true;
    }

    private static void HandleDefuse(GamePlayerOwner owner, Player player, float time)
    {
        if (H.Session.matchState is not MatchState.RoundPlanted)
            return;

        if (player.CurrentState is IdleStateClass)
        {
            Vector3 pos = H.BombHandler.BombPlantedPosition;

            Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.Defusing, pos);

            owner.ShowObjectivesPanel("Defusing {0:F1}", time);

            player.CurrentManagedState.Plant(true, false, time, async (success) =>
            {
                owner.CloseObjectivesPanel();

                if (!success)
                {
                    Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.Planted, pos);
                    return;
                }

                Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.Defused, pos);
                owner.ClearInteractionState();
            });
        }
        else
        {
            owner.DisplayPreloaderUiNotification("You can't defuse while moving");
        }
    }

    private static void HandlePlant(GamePlayerOwner owner, Player player, float time)
    {
        if (player.CurrentState is IdleStateClass)
        {
            Vector3 pos = GetBombPlantPosition(player);

            Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.Planting, pos);

            owner.ShowObjectivesPanel("Planting {0:F1}", time);

            player.CurrentManagedState.Plant(true, false, time, async (success) =>
            {
                owner.CloseObjectivesPanel();

                if (!success)
                {
                    Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.None, pos);
                    return;
                }

                Singleton<BombStatePacketWarden>.Instance.Send(player, BombState.Planted, pos);
                player.TryPopContainedItem(EquipmentSlot.Backpack, false).Forget();

                owner.ClearInteractionState();
            });
        }
        else
        {
            owner.DisplayPreloaderUiNotification("You can't plant while moving");
        }
    }

    private static bool TryFindItem(string templateId, out Item item)
    {
        item = H.MainPlayer.Profile.Inventory
            .GetPlayerItems(EPlayerItems.InRaidItems)
            .FirstOrDefault(i => i.TemplateId == templateId);

        return item != null;
    }

    private static Vector3 GetBombPlantPosition(Player player)
    {
        return Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 1f)
            ? hit.point
            : player.Position;
    }
}