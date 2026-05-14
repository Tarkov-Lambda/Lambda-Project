using System;
using Comfort.Common;
using EFT;
using UnityEngine;
using EFT.CameraControl;
using System.Collections.Generic;
using Lambda.Core.Main.Gamemode;
using System.Linq;
using Lambda.Core.Networking;
using Lambda.Core.Main.Dying;
using Cysharp.Threading.Tasks;
using EFT.UI;
using EFT.InputSystem;

namespace Lambda.Core.Main;

public class SpectatorManager : Singleton<SpectatorManager>, IDisposable
{
    private Player observedPlayer = null;
    Transform observedPlayerCameraTransform = null;
    private FakeCorpse observedCorpse = null;

    public static event Action<Player> OnSelfStartSpectating;
    public static event Action OnSelfStopSpectating;

    public SpectatorManager()
    {
        if (H.IsHeadless) return;
        UnityTicker.OnLateUpdate += onUpdate;
        EventBus.OnEnter += OnEnter;
        EventBus.OnSelfFactionChanged += OnFactionChanged;
        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied += OnPlayerKilled;
    }

    public void Dispose()
    {
        UnityTicker.OnLateUpdate -= onUpdate;
        EventBus.OnEnter -= OnEnter;
        EventBus.OnSelfFactionChanged -= OnFactionChanged;
        Singleton<PlayerKilledPacketWarden>.Instance.AfterPacketApplied -= OnPlayerKilled;
        StopSpectating();
        Release(this);
    }

    private void ChangeBattleUIPOV(Player player)
    {
        ItemUiContext.Instance.Configure(
            player.InventoryController,
            player.Profile,
            ItemUiContext.Instance.Session,
            ItemUiContext.Instance.Session?.InsuranceCompany,
            null,
            player.HealthController,
            ItemUiContext.Instance.CompoundItem_0,
            ItemUiContext.Instance.ContextType,
            ECursorResult.Ignore,
            null,
            player.Equipment,
            player.AbstractQuestControllerClass
        );
    }

    private void OnPlayerKilled(PlayerKilledPacket packet)
    {
        if (observedPlayer == null || packet.Player != observedPlayer) return;

        // Fetch the ragdoll from the creator to follow the death
        observedCorpse = H.RagdollCreator.GetCorpse(packet.Player);
        if (observedCorpse != null)
        {
            observedCorpse.SetAttachedCamera(CameraClass.Instance.Camera);
            WaitAndSwitchPlayer(observedPlayer.Id).Forget();
        }
    }

    private async UniTaskVoid WaitAndSwitchPlayer(int victimId)
    {
        _ = PU.CloseEyes(false, false);
        await UniTask.Delay(4000);

        // Make sure we haven't manually swapped players while waiting
        if (observedPlayer != null && observedPlayer.Id == victimId && observedCorpse != null)
        {
            SwitchSpectatePlayer();
        }
    }

    private void OnFactionChanged(Faction faction)
    {
        if (faction == Faction.Spectator)
            SwitchSpectatePlayer();
        else
            StopSpectating();
    }

    private void OnEnter(MatchState matchState)
    {
        if (matchState is MatchState.Cleanup or MatchState.RoundPrepare)
        {
            StopSpectating();
        }
    }

    private void onUpdate()
    {
        if (observedPlayer == null) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            SwitchSpectatePlayer();
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SwitchSpectatePlayer(false);
        }

        // Suspend modifying the camera if we are currently watching a corpse fall
        // Let FakeCorpse.Update() drive the camera directly.
        if (observedCorpse != null) return;

        Transform mainCameraTransform = CameraClass.Instance.Camera.transform;
        Vector3 offset = observedPlayerCameraTransform.position;
        offset.y += 0.05f;

        mainCameraTransform.position = offset;
        mainCameraTransform.rotation = observedPlayerCameraTransform.rotation;
    }

    public void SwitchSpectatePlayer(bool next = true)
    {
        if (H.IsHeadless) return;

        List<PlayerScore> validPlayersToSpectate = new List<PlayerScore>();

        // Case 1: You are a dedicated Spectator
        if (H.MainPlayerScore.Faction == Faction.Spectator)
        {
            validPlayersToSpectate = H.Scoreboard.Values
                .Where(s => s.Faction != Faction.Spectator && s.IsAlive == true)
                .ToList();
        }
        else
        {
            // First, try to find living teammates.
            validPlayersToSpectate = H.AllTeammateScores
                .Where(s => s.Faction != Faction.Spectator && s.IsAlive == true)
                .ToList();

            // FALLBACK: If everyone in your faction is dead, get everyone else who isn't a spectator
            // if (validPlayersToSpectate.Count == 0)
            // {
            //     validPlayersToSpectate = H.Scoreboard.Values
            //         .Where(s => s.Faction != Faction.Spectator && s.IsAlive == true)
            //         .ToList();
            // }
        }

        if (validPlayersToSpectate.Count == 0)
        {
            StopSpectating();
            return;
        }

        int currentIndex;
        if (observedPlayer != null)
        {

            currentIndex = validPlayersToSpectate.FindIndex(s => s.player.Id == observedPlayer.Id);
        }
        else
        {
            currentIndex = -1;
        }

        // Calculate next index
        if (next)
        {
            currentIndex++;
            if (currentIndex >= validPlayersToSpectate.Count || currentIndex < 0)
                currentIndex = 0;
        }
        else
        {
            currentIndex--;
            if (currentIndex < 0)
                currentIndex = validPlayersToSpectate.Count - 1;
        }

        SpectatePlayer(validPlayersToSpectate[currentIndex].player);
    }

    public void SpectatePlayer(Player player)
    {
        if (player.IsYourPlayer) return;

        if (observedPlayer != null)
        {
            StopSpectating();
        }

        observedPlayer = player;
        PU.OpenEyes();

        SetHandsSkin(observedPlayer);

        UpdatePointOfView(observedPlayer, EPointOfView.FirstPerson);
        ChangeCameraPOV(observedPlayer);

        ChangeBattleUIPOV(observedPlayer);

        OnSelfStartSpectating?.Invoke(observedPlayer);
    }

    public void SetHandsSkin(Player player)
    {
        var observedPlayerHandsKnown = observedPlayer.PlayerBody.BodyCustomization.TryGetValue(EBodyModelPart.Hands, out MongoID observedPlayerHandsId);
        ResourceKey selectedHandsBundle = null;

        if (observedPlayerHandsKnown)
        {
            ResourceKey observedHandsBundle = H.CustomizationSolverClass.GetBundle(observedPlayerHandsId);
            if (observedHandsBundle.IsReadyNow())
            {
                selectedHandsBundle = observedHandsBundle;
            }
        }

        // Fallback to Main Player Hands
        if (selectedHandsBundle == null)
        {
            H.MainPlayer.PlayerBody.BodyCustomization.TryGetValue(EBodyModelPart.Hands, out MongoID mainPlayerHandsId);
            selectedHandsBundle = H.CustomizationSolverClass.GetBundle(mainPlayerHandsId);
        }

        var handsKvp = new KeyValuePair<EBodyModelPart, ResourceKey>(EBodyModelPart.Hands, selectedHandsBundle);
        observedPlayer.PlayerBody.SetSkin(handsKvp, observedPlayer.PlayerBody.SkeletonHands);
    }

    public void StopSpectating()
    {
        if (!H.IsInRaid()) return;
        if (H.IsHeadless) return;

        if (observedPlayer != null)
        {
            UpdatePointOfView(observedPlayer, EPointOfView.ThirdPerson);
        }

        if (observedCorpse != null)
        {
            observedCorpse.SetAttachedCamera(null);
            observedCorpse = null;
        }

        observedPlayer = null;

        ChangeBattleUIPOV(H.MainPlayer);

        ChangeCameraPOV(H.MainPlayer);
        OnSelfStopSpectating?.Invoke();
    }

    private void ChangeCameraPOV(Player player)
    {
        CameraClass.Instance.SetPlayer(player);

        PlayerCameraController playerCameraController = H.MainPlayer.GetComponent<PlayerCameraController>();
        playerCameraController.enabled = player.IsYourPlayer;
        observedPlayerCameraTransform = player.IsYourPlayer ? null : player.ProceduralWeaponAnimation.HandsContainer.CameraTransform;
    }

    private bool UpdatePointOfView(Player player, EPointOfView pointOfView)
    {
        if (!(player.PlayerBody == null))
        {
            if (pointOfView != player.PlayerBody.PointOfView.Value)
            {
                player.PlayerBody.PointOfView.Value = pointOfView;
                player.PlayerBody.UpdatePlayerRenders(pointOfView, player.Side);
                method_22(player);
                return true;
            }
        }
        return false;
    }

    private void method_22(Player player)
    {
        player.ProceduralWeaponAnimation.SetFovParams(1f);

        if (player.PlayerBody.PointOfView.Value == EPointOfView.ThirdPerson)
        {
            player.PlayerBones.Ribcage.Original.localScale = new Vector3(1f, 1f, 1f);
        }

        player.ProceduralWeaponAnimation.Overweight = 0f;
        player.ProceduralWeaponAnimation.PointOfView = player.PlayerBody.PointOfView;

        if (player.HealthController.IsAlive && player.PlayerBody.PointOfView.Value.IsFirstPerson())
        {
            player.ProceduralWeaponAnimation.UpdateWeaponVariables();
            player.ProceduralWeaponAnimation.ResetSpring();

            player.ProceduralWeaponAnimation.FindAimTransforms();

            if (player.ProceduralWeaponAnimation.ScopeAimTransforms.Count > 0)
            {
                player.ProceduralWeaponAnimation.OnScopesModeUpdated();
            }
        }
    }
}