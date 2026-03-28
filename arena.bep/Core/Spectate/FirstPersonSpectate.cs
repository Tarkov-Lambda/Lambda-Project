using System;
using Comfort.Common;
using EFT;
using EFT.Animations;
using UnityEngine;
using EFT.CameraControl;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.Rendering;
using ifp.arena.bep.Core.Gamemode;


namespace ifp.arena.bep.Core
{
    public class SpectatorManager : Singleton<SpectatorManager>, IDisposable
    {
        private Player observedPlayer = null;

        public SpectatorManager()
        {
            EventBus.OnLateUpdate += onUpdate;
        }

        public void onUpdate()
        {
            if (observedPlayer == null) return;

            Transform mainCameraTransform = CameraClass.Instance.Camera.transform;
            BifacialTransform observedPlayerCameraTransform = observedPlayer.PlayerBones.Head;
            Quaternion offset = Quaternion.Euler(-90f, 180f, -90f); // try -90 if wrong direction

            mainCameraTransform.transform.position = observedPlayerCameraTransform.position;
            mainCameraTransform.rotation = observedPlayerCameraTransform.rotation * offset;
        }

        public void SpectatePlayer(Player player)
        {
            if (observedPlayer != null)
            {
                StopSpectating();
            }

            observedPlayer = player;

            if (H.MainPlayer.PlayerBody.BodyCustomization.TryGetValue(EBodyModelPart.Hands, out MongoID handsId))
            {
                var customizationSolver = Singleton<CustomizationSolverClass>.Instance;
                ResourceKey handsBundle = customizationSolver.GetBundle(handsId);

                if (handsBundle != null)
                {
                    var handsKvp = new KeyValuePair<EBodyModelPart, ResourceKey>(EBodyModelPart.Hands, handsBundle);
                    observedPlayer.PlayerBody.SetSkin(handsKvp, observedPlayer.PlayerBody.SkeletonHands);
                }
            }

            UpdatePointOfView(observedPlayer, EPointOfView.FirstPerson);

            ChangeCameraPOV(observedPlayer);
        }

        private void ChangeCameraPOV(Player player)
        {
            CameraClass.Instance.SetPlayer(player);

            PlayerCameraController playerCameraController = H.MainPlayer.GetComponent<PlayerCameraController>();
            playerCameraController.enabled = player.IsYourPlayer;
        }


        public void StopSpectating()
        {
            if (observedPlayer != null)
            {
                UpdatePointOfView(observedPlayer, EPointOfView.ThirdPerson);
            }

            observedPlayer = null;

            ChangeCameraPOV(H.MainPlayer);
        }

        // Token: 0x06018A7A RID: 100986 RVA: 0x00724CA4 File Offset: 0x00722EA4
        public bool UpdatePointOfView(Player player, EPointOfView pointOfView)
        {
            if (!(player.PlayerBody == null))
            {
                if (pointOfView != player.PlayerBody.PointOfView.Value)
                {
                    player.PlayerBody.PointOfView.Value = pointOfView;
                    player.PlayerBody.UpdatePlayerRenders(pointOfView, player.Side);
                    // player.\uE003();
                    UpdateSomething(player);
                    return true;
                }
            }
            return false;
        }

        // \uE003
        // Token: 0x06018A7B RID: 100987 RVA: 0x00724D0C File Offset: 0x00722F0C
        private void UpdateSomething(Player player)
        {
            // player.\uE004((float) Singleton<\uEB6A>.Instance.Game.Settings.FieldOfView);
            // player.\uE006(false);
            if (player.PlayerBody.PointOfView.Value == EPointOfView.ThirdPerson)
            {
                player.PlayerBones.Ribcage.Original.localScale = new Vector3(1f, 1f, 1f);
            }
            // player.\uE005(player.PlayerBody.PointOfView.Value);
            player.ProceduralWeaponAnimation.Overweight = 0f;
            player.ProceduralWeaponAnimation.PointOfView = player.PlayerBody.PointOfView;
            if (player.HealthController.IsAlive)

            {
                // if (player.PlayerBody.PointOfView.Value == EPointOfView.ThirdPerson && player.IsOutToIdleAnimatorState())
                // {
                //     player.SetOutToIdleEndAnimatorState();
                // }
                if (player.PlayerBody.PointOfView.Value.IsFirstPerson())
                {
                    player.ProceduralWeaponAnimation.UpdateWeaponVariables();
                    player.ProceduralWeaponAnimation.ResetSpring();
                    // player.ProceduralWeaponAnimation.ResetOptics();
                    player.ProceduralWeaponAnimation.FindAimTransforms();
                    if (player.ProceduralWeaponAnimation.ScopeAimTransforms.Count > 0)
                    {
                        player.ProceduralWeaponAnimation.OnScopesModeUpdated();
                    }
                }
                // WeaponPrefab currentWeaponPrefab = player.ObservedPlayerController.HandsController.CurrentWeaponPrefab;
                // if (currentWeaponPrefab != null)
                // {
                //     currentWeaponPrefab.OnChangePointOfView(player);
                // }
                player.LateUpdate();
            }
        }

        // \uE005
        // Token: 0x06018A7D RID: 100989 RVA: 0x00724F1C File Offset: 0x0072311C
        private void changeBundleAnimationBones(Player player, EPointOfView pointOfView)
        {
            bool flag = pointOfView == EPointOfView.ThirdPerson;
            // player.BundleAnimationBones.BodyAnimator.SetBool(PlayerAnimator.THIRDPERSON_HASH, flag);
            // player.BundleAnimationBones.BodyAnimator.SetFloat(PlayerAnimator.THIRDPERSON_FLOAT_HASH, flag ? 1f : 0f);
            // player.BundleAnimationBones.BodyAnimator.SetLayerWeight(1, (float)(flag ? 1 : 0));
        }

        // \uE006
        // Token: 0x06018A7E RID: 100990 RVA: 0x00724F88 File Offset: 0x00723188
        private void fovSomething(Player player, bool force = false)
        {
            player.RibcageScaleCurrentTarget = 1f; // player.\uE034
            if (force)
            {
                player.RibcageScaleCurrent = player.RibcageScaleCurrentTarget;
                player.ProceduralWeaponAnimation.ResetFovAdjustments(player);
            }
            // player.ProceduralWeaponAnimation.SetFovParams(player.\uE034, player.\uE035); // player.\uE034, player.\uE035
        }

        public void Dispose()
        {
            StopSpectating();
            Release(this);
        }
    }
}