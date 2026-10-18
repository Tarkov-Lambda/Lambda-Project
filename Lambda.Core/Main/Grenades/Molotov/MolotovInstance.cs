using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using EFT.Interactive;
using Lambda.Core.Main.FX;
using Lambda.Core.Networking;
using PacketWarden.TimeSync;
using UnityEngine;

namespace Lambda.Core.Main;

public class MolotovInstance : MonoBehaviour
{
    private CancellationTokenSource _cts;
    private const float CleanupDelayAfterDuration = 5f;

    private static readonly List<ParticleSystem> s_particleSystems = new();

    private BetterSource _loopSource;
    private Vector3 CentroidPosition;

    public void Initialize(MolotovExplosionPacket packet)
    {
        _cts = new CancellationTokenSource();

        CentroidPosition = GetFireCentroid(packet.fireNodes);

        H.BetterAudio.TryPlayAtPoint(
            out _,
            CentroidPosition,
            H.AudioHandler.PrefabSounds.MolotovExplosion,
            BetterAudio.AudioSourceGroupType.Environment,
            25,
            1f,
            EOcclusionTest.ContinuousPropagated,
            null,
            true,
            true,
            true,
            true
        );

        StartBurnAudio();

        SpawnNodesAsync(packet.fireNodes, packet.fireRadius, packet.Timestamp).Forget();

        DestroySelfAfterDurationAsync(packet.Timestamp).Forget();
    }

    private void StartBurnAudio()
    {
        bool success = H.BetterAudio.TryPlayAtPoint(
            out _loopSource,
            CentroidPosition,
            H.AudioHandler.PrefabSounds.MolotovBurning,
            BetterAudio.AudioSourceGroupType.Environment,
            25,
            1f,
            EOcclusionTest.ContinuousPropagated,
            null,
            true,
            false,
            false,
            false
        );

        if (_loopSource == null)
            return;

        _loopSource.Loop = true;

        float currentVolume = 0f;
        DOTween
            .To(
                () => currentVolume,
                x =>
                {
                    currentVolume = x;
                    if (_loopSource != null)
                        _loopSource.SetBaseVolume(x);
                },
                1f,
                1.5f
            )
            .SetTarget(this);
    }

    // Nodes arrive sorted by TimeOffset, so a single sequential wait covers all of them
    private async UniTaskVoid SpawnNodesAsync(
        FireNode[] nodes,
        float radius,
        double packetTimestamp
    )
    {
        if (nodes == null)
            return;

        foreach (var node in nodes)
        {
            float delay = node.TimeOffset - (float)(NetworkTime.ServerNowSeconds - packetTimestamp);

            if (delay > 0)
            {
                bool canceled = await UniTask
                    .Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: _cts.Token)
                    .SuppressCancellationThrow();

                if (canceled)
                    return;
            }

            if (NetworkTime.ServerNowSeconds - packetTimestamp >= MolotovController.duration)
                return;

            SpawnEffect(node, radius);
        }
    }

    private void SpawnEffect(FireNode node, float radius)
    {
        GameObject fireEffect = Instantiate(
            FXHandler.Instance.FireNodeEffectPrefab,
            node.Position,
            Quaternion.FromToRotation(Vector3.up, node.Normal),
            transform
        );

        fireEffect.transform.Rotate(0f, Random.Range(0f, 360f), 0f, Space.Self);

        float scaleVariance = Random.Range(0.85f, 1.15f);
        float scaleMultiplier = radius / 0.2f * scaleVariance;
        fireEffect.transform.localScale = Vector3.one * scaleMultiplier;

        fireEffect.GetComponentsInChildren(s_particleSystems);
        foreach (var ps in s_particleSystems)
        {
            var main = ps.main;
            main.simulationSpeed *= Random.Range(0.8f, 1.25f);
            main.startLifetimeMultiplier *= Random.Range(0.8f, 1.2f);
            main.startSizeMultiplier *= Random.Range(0.8f, 1.2f);
            main.startDelayMultiplier += Random.Range(0f, 0.1f);
        }

        fireEffect.GetOrAddComponent<FlameDamageTrigger>();
    }

    private async UniTask DestroySelfAfterDurationAsync(double packetTimestamp)
    {
        double elapsed = NetworkTime.ServerNowSeconds - packetTimestamp;
        float remainingTime = MolotovController.duration - (float)elapsed;

        if (remainingTime > 0)
        {
            bool canceled = await UniTask
                .Delay(System.TimeSpan.FromSeconds(remainingTime), cancellationToken: _cts.Token)
                .SuppressCancellationThrow();
            if (canceled)
                return;
        }

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (var light in GetComponentsInChildren<Light>())
            light.enabled = false;

        StopBurnAudio();

        bool fadeCanceled = await UniTask
            .Delay(
                System.TimeSpan.FromSeconds(CleanupDelayAfterDuration),
                cancellationToken: _cts.Token
            )
            .SuppressCancellationThrow();
        if (fadeCanceled)
            return;

        ReleaseLoopSource();

        Destroy(gameObject);
    }

    private void StopBurnAudio()
    {
        H.BetterAudio.TryPlayAtPoint(
            out _,
            CentroidPosition,
            H.AudioHandler.PrefabSounds.MolotovExtinquished,
            BetterAudio.AudioSourceGroupType.Environment,
            25,
            1f,
            EOcclusionTest.ContinuousPropagated,
            null,
            true,
            true,
            true,
            true
        );

        if (_loopSource == null)
            return;

        float currentVolume = 1f;

        DOTween
            .To(
                () => currentVolume,
                x =>
                {
                    currentVolume = x;
                    _loopSource.SetBaseVolume(x);
                },
                0f,
                1f
            )
            .SetTarget(this)
            .OnComplete(() => ReleaseLoopSource());
    }

    private void ReleaseLoopSource()
    {
        if (_loopSource == null)
            return;

        _loopSource.source1.loop = false;
        _loopSource.Release();
        _loopSource = null;
    }

    private Vector3 GetFireCentroid(FireNode[] nodes)
    {
        if (nodes == null || nodes.Length == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;

        foreach (var node in nodes)
            sum += node.Position;

        return sum / nodes.Length;
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);

        ReleaseLoopSource();

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
