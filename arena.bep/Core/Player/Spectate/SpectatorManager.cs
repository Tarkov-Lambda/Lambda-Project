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

namespace ifp.arena.bep.Core;

public class SpectatorManager : Singleton<SpectatorManager>, IDisposable
{
    private Player observedPlayer = null;
    Transform observedPlayerCameraTransform = null;

    public static event Action<Player> OnSelfStartSpectating;
    public static event Action OnSelfStopSpectating;

    public SpectatorManager()
    {
        if (H.IsHeadless) return;
        EventBus.OnLateUpdate += onUpdate;
        EventBus.OnEnter += OnEnter;
        EventBus.OnSelfFactionChanged += OnFactionChanged;
    }

    public void Dispose()
    {
        EventBus.OnLateUpdate -= onUpdate;
        EventBus.OnEnter -= OnEnter;
        EventBus.OnSelfFactionChanged -= OnFactionChanged;
        StopSpectating();
        Release(this);
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
        if (matchState == MatchState.RoundPrepare)
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

        Transform mainCameraTransform = CameraClass.Instance.Camera.transform;
        Vector3 offset = observedPlayerCameraTransform.position;
        offset.y += 0.05f;

        mainCameraTransform.position = offset;
        mainCameraTransform.rotation = observedPlayerCameraTransform.rotation;
    }

    public void SwitchSpectatePlayer(bool next = true)
    {
        if (H.IsHeadless) return;
        List<PlayerScore> validPlayersToSpectate;

        if (H.MainPlayerScore.Faction == Faction.Spectator)
        {
            validPlayersToSpectate = H.Scoreboard.Values.Where(s => s.Faction != Faction.Spectator).ToList();
        }
        else validPlayersToSpectate = H.AllTeammateScores;

        if (validPlayersToSpectate.Count == 0)
        {
            StopSpectating();
            return;
        }

        int currentIndex;
        if (observedPlayer != null)
        {
            currentIndex = validPlayersToSpectate.IndexOf(H.GetPlayerScore(observedPlayer.Id));
        }
        else currentIndex = 0;


        if (next)
        {
            currentIndex++;
            if (currentIndex >= validPlayersToSpectate.Count)
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

        OnSelfStartSpectating?.Invoke(observedPlayer);
    }



    public void StopSpectating()
    {
        if (H.IsHeadless) return;
        
        if (observedPlayer != null)
        {
            UpdatePointOfView(observedPlayer, EPointOfView.ThirdPerson);
        }

        observedPlayer = null;

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

            // // Force the weapon prefab to swap its LODs to First-Person mode
            // if (player.HandsController is EFT.Player.FirearmController firearmController)
            // {
            //     if (firearmController.WeaponPrefab != null)
            //     {
            //         // This swaps the weapon from 3rd person LODs to 1st person LODs
            //         firearmController.WeaponPrefab.OnChangePointOfView(player);
            //     }
            // }
        }
    }


    // Token: 0x0600EF88 RID: 61320 RVA: 0x0064F7E0 File Offset: 0x0064D9E0
    // public void ResetOptics(Player player)
    // {
    //     foreach (ProceduralWeaponAnimation.SightNBone sightNBone in player.ProceduralWeaponAnimation._optics)
    //     {
    //         global::UnityEngine.Object @object;
    //         if (sightNBone == null)
    //         {
    //             @object = null;
    //         }
    //         else
    //         {
    //             ScopePrefabCache scopePrefabCache = sightNBone.ScopePrefabCache;
    //             @object = ((scopePrefabCache != null) ? scopePrefabCache.CurrentModOpticSight : null);
    //         }
    //         if (@object != null)
    //         {
    //             sightNBone.ScopePrefabCache.CurrentModOpticSight.enabled = false;
    //             sightNBone.ScopePrefabCache.CurrentModOpticSight.LensFade(true);
    //         }
    //     }
    // }


    // Token: 0x06018A7C RID: 100988 RVA: 0x00837D70 File Offset: 0x00835F70
    private void method_23(Player player, float fov)
    {
        // float num = Mathf.InverseLerp((float)GClass1155.MinFieldOfView, (float)GClass1155.MaxFieldOfView, fov);
        // float num2 = 1f;
        // float num3 = 0.65f;
        // ArenaSessionStaticData arenaSessionStaticData;
        // if (GClass3310.TryGetData<ArenaSessionStaticData>(out arenaSessionStaticData) && arenaSessionStaticData.GraphicSettings != null)
        // {
        //     num2 = arenaSessionStaticData.GraphicSettings.RibcageScaleCompensatedByMinFov;
        //     num3 = arenaSessionStaticData.GraphicSettings.RibcageScaleCompensatedByMaxFov;
        // }
        // player.float_6 = Mathf.Lerp(num2, num3, num);
        // player.float_7 = num;
    }

    // Token: 0x06018A7D RID: 100989 RVA: 0x00837DDC File Offset: 0x00835FDC
    private void method_24(Player player, EPointOfView pointOfView)
    {
        // bool isThirdPerson = pointOfView == EPointOfView.ThirdPerson;
        // player.BundleAnimationBones.BodyAnimator.SetBool(PlayerAnimator.THIRDPERSON_HASH, flag);
        // player.BundleAnimationBones.BodyAnimator.SetFloat(PlayerAnimator.THIRDPERSON_FLOAT_HASH, flag ? 1f : 0f);
        // player.BundleAnimationBones.BodyAnimator.SetLayerWeight(1, (float)(flag ? 1 : 0));
    }

    // Token: 0x06018A7E RID: 100990 RVA: 0x002BACDD File Offset: 0x002B8EDD
    private void method_25(Player player, bool force = false)
    {
        // player.RibcageScaleCurrentTarget = player.float_6;
        // if (force)
        // {
        //     player.RibcageScaleCurrent = player.RibcageScaleCurrentTarget;
        //     player.ProceduralWeaponAnimation.ResetFovAdjustments(player);
        // }
        // player.ProceduralWeaponAnimation.SetFovParams(player.float_6, player.float_7);
    }
}