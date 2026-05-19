using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Lambda.Core.Main.FX;
using Lambda.Core.Networking;
using UnityEngine;
using DG.Tweening;
using PacketWarden.TimeSync;
using EFT.Interactive;

namespace Lambda.Core.Main;

public class MolotovInstance : MonoBehaviour
{
    private CancellationTokenSource _cts;
    private const float CleanupDelayAfterDuration = 5f;

    private BetterSource _loopSource;
    private Vector3 CentroidPosition;

    public void Initialize(MolotovExplosionPacket packet)
    {
        _cts = new CancellationTokenSource();

        CentroidPosition = GetFireCentroid(packet.fireNodes);

        H.BetterAudio.TryPlayAtPoint(
            out BetterSource Ignition,
            CentroidPosition,
            H.AudioHandler.PrefabSounds.MolotovExplosion,
            BetterAudio.AudioSourceGroupType.Grenades,
            50,
            1f,
            EOcclusionTest.None,
            null,
            true,
            true,
            true,
            true
        );

        StartBurnAudio();

        SpawnNodesAsync(packet).Forget();

        DestroySelfAfterDurationAsync(packet.Timestamp).Forget();
    }

    private void StartBurnAudio()
    {
        bool success = H.BetterAudio.TryPlayAtPoint(
            out _loopSource,
            CentroidPosition,
            H.AudioHandler.PrefabSounds.MolotovBurning,
            BetterAudio.AudioSourceGroupType.Grenades,
            50,
            0f,
            EOcclusionTest.None,
            null,
            true,
            false,
            false,
            true
        );

        if (success && _loopSource != null)
        {
            _loopSource.source1.loop = true;

            float currentVolume = 0f;
            DOTween.To(() => currentVolume, x =>
            {
                currentVolume = x;
                if (_loopSource != null)
                    _loopSource.SetBaseVolume(x);
            }, 1f, 1.5f)
            .SetTarget(this);
        }
    }

    private async UniTask SpawnNodesAsync(MolotovExplosionPacket packet)
    {
        var tasks = new List<UniTask>(packet.fireNodes.Count);

        foreach (var node in packet.fireNodes)
        {
            tasks.Add(SpawnSingleNodeAsync(node, packet.Timestamp));
        }

        await UniTask.WhenAll(tasks);
    }

    private async UniTask SpawnSingleNodeAsync(FireNode node, double packetTimestamp)
    {
        double elapsed = NetworkTime.ServerNowSeconds - packetTimestamp;

        float delay = node.TimeOffset - (float)elapsed;

        if (delay > 0)
        {
            bool canceled = await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                cancellationToken: _cts.Token
            ).SuppressCancellationThrow();

            if (canceled) return;
        }

        elapsed = NetworkTime.ServerNowSeconds - packetTimestamp;

        if (elapsed >= MolotovController.duration)
            return;

        SpawnEffect(node);
    }

    private void SpawnEffect(FireNode node)
    {
        GameObject fireEffect = Instantiate(FXHandler.Instance.FireNodeEffectPrefab, node.Position, node.Rotation, this.transform);

        fireEffect.transform.Rotate(0f, Random.Range(0f, 360f), 0f, Space.Self);

        float scaleVariance = Random.Range(0.85f, 1.15f);
        float scaleMultiplier = (node.Radius / 0.2f) * scaleVariance;
        fireEffect.transform.localScale = Vector3.one * scaleMultiplier;

        ParticleSystem[] particleSystems = fireEffect.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
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
            bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(remainingTime), cancellationToken: _cts.Token).SuppressCancellationThrow();
            if (canceled) return;
        }

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (var light in GetComponentsInChildren<Light>()) light.enabled = false;

        StopBurnAudio();

        bool fadeCanceled = await UniTask.Delay(System.TimeSpan.FromSeconds(CleanupDelayAfterDuration), cancellationToken: _cts.Token).SuppressCancellationThrow();
        if (fadeCanceled) return;

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
            50,
            1f,
            EOcclusionTest.None,
            null, true, true, true, true
        );

        // 2. Fade out the looping sound
        if (_loopSource != null)
        {
            float currentVolume = 1f;
            DOTween.To(() => currentVolume, x =>
            {
                currentVolume = x;
                if (_loopSource != null) _loopSource.SetBaseVolume(x);
            }, 0f, 1f) // Fade out over 1 second
            .SetTarget(this)
            .OnComplete(() =>
            {
                ReleaseLoopSource();
            });
        }
    }

    private void ReleaseLoopSource()
    {
        if (_loopSource != null)
        {
            _loopSource.source1.loop = false;
            _loopSource.Release();
            _loopSource = null;
        }
    }

    private Vector3 GetFireCentroid(List<FireNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return transform.position;

        Vector3 sum = Vector3.zero;

        foreach (var node in nodes)
            sum += node.Position;

        return sum / nodes.Count;
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