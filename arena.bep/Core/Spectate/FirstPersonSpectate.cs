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
using Fika.Core.Main.Players;


namespace ifp.arena.bep.Core
{
    public class SpectatorManager : Singleton<SpectatorManager>, IDisposable
    {
        private Player observedPlayer = null;

        // interpolation
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _interpTime;
        private const float PacketInterval = 1f / 20f;

        public SpectatorManager()
        {
            EventBus.OnLateUpdate += onUpdate;
        }

        public void onUpdate()
        {
            if (observedPlayer == null) return;

            Transform mainCameraTransform = CameraClass.Instance.Camera.transform;
            Transform observedPlayerCameraTransform = observedPlayer.Transform.Original.FindTransform("Cam");

            if (observedPlayerCameraTransform != null)
            {
                // Direct assignment! The bone is smoothed automatically by the local Animator & PWA.
                mainCameraTransform.position = observedPlayerCameraTransform.position;
                mainCameraTransform.rotation = observedPlayerCameraTransform.rotation;
            }

            // Transform mainCameraTransform = CameraClass.Instance.Camera.transform;
            // Transform observedPlayerCameraTransform = observedPlayer.Transform.Original.FindTransform("Cam");

            // Vector3 snapshotPos = observedPlayerCameraTransform.position;
            // Quaternion snapshotRot = observedPlayerCameraTransform.rotation;

            // // Detect a new network snapshot arriving (transform changed from what we last knew)
            // if (snapshotPos != _targetPosition || snapshotRot != _targetRotation)
            // {
            //     // Start the next blend from wherever the camera currently sits (no pop)
            //     _prevPosition = mainCameraTransform.position;
            //     _prevRotation = mainCameraTransform.rotation;
            //     _targetPosition = snapshotPos;
            //     _targetRotation = snapshotRot;
            //     _interpTime = 0f;
            // }

            // _interpTime = Mathf.Min(_interpTime + Time.deltaTime / PacketInterval, 1f);

            // mainCameraTransform.position = Vector3.Lerp(_prevPosition, _targetPosition, _interpTime);
            // mainCameraTransform.rotation = Quaternion.Slerp(_prevRotation, _targetRotation, _interpTime);
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

            // Seed snapshot state so interpolation starts from the current position
            Transform camTransform = observedPlayer.Transform.Original.FindTransform("Cam");
            if (camTransform != null)
            {
                _prevPosition = camTransform.position;
                _prevRotation = camTransform.rotation;
                _targetPosition = camTransform.position;
                _targetRotation = camTransform.rotation;
                _interpTime = 1f;
            }
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
            bool isThirdPerson = pointOfView == EPointOfView.ThirdPerson;

            // Grab the base Animator
            Animator bodyAnimator = player.GetComponentInChildren<Animator>();
            if (bodyAnimator != null)
            {
                D.Dump(bodyAnimator);
                // In EFT, Layer 1 is the Third-Person layer. 
                // We MUST set it to 0 for First-Person, otherwise it pushes the gun off-center!
                bodyAnimator.SetLayerWeight(1, isThirdPerson ? 1f : 0f);

                // Standard EFT animator hashes for point of view
                int tpHash = Animator.StringToHash("IsThirdPerson");
                int tpFloatHash = Animator.StringToHash("ThirdPerson");

                bodyAnimator.SetBool(tpHash, isThirdPerson);
                bodyAnimator.SetFloat(tpFloatHash, isThirdPerson ? 1f : 0f);
            }
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

        public void Dispose()
        {
            StopSpectating();
            Release(this);
        }
    }
}

