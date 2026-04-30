using System;
using Comfort.Common;
using EFT;
using UnityEngine;
using EFT.CameraControl;
using System.Collections.Generic;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.GameTypes;
using ifp.arena.shared;
using System.Linq;
using ifp.arena.bep.networking;
using ifp.arena.bep.Core.Dying;
using Cysharp.Threading.Tasks;
using EFT.UI;
using EFT.InputSystem;

namespace ifp.arena.bep.Core;

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
        PlayerKilledPacketHandler.AfterPacketApplied += OnPlayerKilled;
    }

    public void Dispose()
    {
        UnityTicker.OnLateUpdate -= onUpdate;
        EventBus.OnEnter -= OnEnter;
        EventBus.OnSelfFactionChanged -= OnFactionChanged;
        PlayerKilledPacketHandler.AfterPacketApplied -= OnPlayerKilled;
        StopSpectating();
        Release(this);
    }

    private void SwitchUI(Player player)
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
            // Case 2: You are a player who died. 
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

        // If there is literally no one alive to watch, stop spectating
        if (validPlayersToSpectate.Count == 0)
        {
            StopSpectating();
            return;
        }

        int currentIndex;
        if (observedPlayer != null)
        {
            // Find current player index in the list. IndexOf returns -1 if not found 
            // (which happens when you transition from Teammate list to Global list)
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
        if (H.MainPlayer == null) return;
        if (player.IsYourPlayer) return;

        if (observedPlayer != null)
        {
            StopSpectating();
        }

        observedPlayer = player;
        PU.OpenEyes();

        if (H.MainPlayer.PlayerBody.BodyCustomization.TryGetValue(EBodyModelPart.Hands, out MongoID handsId))
        {
            var customizationSolver = H.CustomizationSolverClass;
            ResourceKey handsBundle = customizationSolver.GetBundle(handsId);

            if (handsBundle != null)
            {
                var handsKvp = new KeyValuePair<EBodyModelPart, ResourceKey>(EBodyModelPart.Hands, handsBundle);
                observedPlayer.PlayerBody.SetSkin(handsKvp, observedPlayer.PlayerBody.SkeletonHands);
            }
        }

        UpdatePointOfView(observedPlayer, EPointOfView.FirstPerson);
        ChangeCameraPOV(observedPlayer);

        SwitchUI(observedPlayer);

        OnSelfStartSpectating?.Invoke(observedPlayer);
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

        SwitchUI(H.MainPlayer);

        ChangeCameraPOV(H.MainPlayer);
        OnSelfStopSpectating?.Invoke();
    }

    private void ChangeCameraPOV(Player player)
    {
        CameraClass.Instance.SetPlayer(player);

        PlayerCameraController playerCameraController = H.MainPlayer.GetComponent<PlayerCameraController>();
        playerCameraController.enabled = player.IsYourPlayer;
        observedPlayerCameraTransform = player.IsYourPlayer ? null : observedPlayer.Transform.Original.FindTransform("Cam");
    }

    // Token: 0x06018A7A RID: 100986 RVA: 0x00724CA4 File Offset: 0x00722EA4
    private bool UpdatePointOfView(Player player, EPointOfView pointOfView)
    {
        if (!(player.PlayerBody == null))
        {
            if (pointOfView != player.PlayerBody.PointOfView.Value)
            {
                player.PlayerBody.PointOfView.Value = pointOfView;
                player.PlayerBody.UpdatePlayerRenders(pointOfView, player.Side);
                // player.\uE003();
                method_22(player);
                return true;
            }
        }
        return false;
    }

    // Token: 0x06018A7B RID: 100987 RVA: 0x00837C04 File Offset: 0x00835E04
    private void method_22(Player player)
    {
        // Default FOV fallback
        // Note: BSG FOV is vertical, 75 vertical is insanely high (like 105 horizontal). Default is ~50.
        float targetFov = CameraClass.Instance.Camera.fieldOfView;

        // Set Ribcage / FOV Compensators
        player.ProceduralWeaponAnimation.SetFovParams(1f);

        if (player.PlayerBody.PointOfView.Value == EPointOfView.ThirdPerson)
        {
            player.PlayerBones.Ribcage.Original.localScale = new Vector3(1f, 1f, 1f);
        }

        // THIS is where we fix the misaligned ADS
        method_24(player, player.PlayerBody.PointOfView.Value);

        player.ProceduralWeaponAnimation.Overweight = 0f;
        player.ProceduralWeaponAnimation.PointOfView = player.PlayerBody.PointOfView;

        if (player.HealthController.IsAlive && player.PlayerBody.PointOfView.Value.IsFirstPerson())
        {
            player.ProceduralWeaponAnimation.UpdateWeaponVariables();
            player.ProceduralWeaponAnimation.ResetSpring();

            // DO NOT comment these out, or red dots/scopes won't turn on or align
            // player.ProceduralWeaponAnimation.ResetOptics();
            player.ProceduralWeaponAnimation.FindAimTransforms();

            if (player.ProceduralWeaponAnimation.ScopeAimTransforms.Count > 0)
            {
                player.ProceduralWeaponAnimation.OnScopesModeUpdated();
            }
        }
    }

    // Token: 0x06018A7C RID: 100988 RVA: 0x00837D70 File Offset: 0x00835F70
    private void method_23(Player player, float fov)
    {
        // float num = Mathf.InverseLerp((float)GClass1155.MinFieldOfView, (float)GClass1155.MaxFieldOfView, fov);
        // ...
    }

    // Token: 0x06018A7D RID: 100989 RVA: 0x00837DDC File Offset: 0x00835FDC
    private void method_24(Player player, EPointOfView pointOfView)
    {
        // bool isThirdPerson = pointOfView == EPointOfView.ThirdPerson;
        // ...
    }

    // Token: 0x06018A7E RID: 100990 RVA: 0x002BACDD File Offset: 0x002B8EDD
    private void method_25(Player player, bool force = false)
    {
        // ...
    }
}