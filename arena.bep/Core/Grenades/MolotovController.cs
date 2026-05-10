using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using EFT.Interactive;
using ifp.arena.bep.Core.FX;
using ifp.arena.bep.networking;
using ifp.arena.bep.networking.TimeSync;
using UnityEngine;

namespace ifp.arena.bep.Core;

public static class MolotovController
{
    public static float duration = 7f;

    public static async UniTask Spawn(MolotovExplosionPacket packet)
    {
        GameObject molotovRoot = new($"MolotovExplosion_{packet.Timestamp}");
        molotovRoot.transform.position = packet.explosionPos;

        MolotovInstance spawner = molotovRoot.AddComponent<MolotovInstance>();
        spawner.Initialize(packet);

        await UniTask.CompletedTask;
    }

    // 6 directions for hexagonal packing
    private static readonly Vector3[] ExpansionDirections = {
        new Vector3(1, 0, 0),
        new Vector3(0.5f, 0, 0.866f),
        new Vector3(-0.5f, 0, 0.866f),
        new Vector3(-1, 0, 0),
        new Vector3(-0.5f, 0, -0.866f),
        new Vector3(0.5f, 0, -0.866f)
    };

    public static List<FireNode> GenerateFireSpread(Vector3 origin)
    {
        List<FireNode> resultNodes = new List<FireNode>(GameplayVariables.vars.MaxNodes);

        Queue<(Vector3 pos, Vector3 normal, int depth)> queue = new Queue<(Vector3, Vector3, int)>();
        HashSet<Vector3Int> visitedGrid = new HashSet<Vector3Int>();

        if (TryFindGround(origin + Vector3.up * 0.5f, out Vector3 startPos, out Vector3 startNormal))
        {
            queue.Enqueue((startPos, startNormal, 0));
            visitedGrid.Add(Quantize(startPos, GameplayVariables.vars.SpreadRadius));
        }
        else return resultNodes;

        while (queue.Count > 0 && resultNodes.Count < GameplayVariables.vars.MaxNodes)
        {
            var current = queue.Dequeue();

            resultNodes.Add(new FireNode
            {
                Position = current.pos,
                Rotation = Quaternion.FromToRotation(Vector3.up, current.normal),
                Radius = GameplayVariables.vars.FireRadius,
                TimeOffset = current.depth * GameplayVariables.vars.TimeBetweenNodes
            });

            foreach (Vector3 dir in ExpansionDirections)
            {
                if (resultNodes.Count + queue.Count >= GameplayVariables.vars.MaxNodes) break;

                Vector3 horizontalOffset = dir * GameplayVariables.vars.SpreadRadius;
                Vector3 rayStart = current.pos + Vector3.up * (GameplayVariables.vars.MaxStepHeight + 0.1f);

                if (Physics.Raycast(rayStart, dir, out RaycastHit wallHit, GameplayVariables.vars.SpreadRadius, 1 << 18)) continue;

                Vector3 downRayStart = current.pos + horizontalOffset + Vector3.up * (GameplayVariables.vars.MaxStepHeight + 0.1f);
                float maxDownDist = GameplayVariables.vars.MaxStepHeight + 0.1f + GameplayVariables.vars.MaxDropHeight;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit groundHit, maxDownDist, 1 << 18))
                {
                    Vector3 newPos = groundHit.point;
                    float heightDiff = newPos.y - current.pos.y;

                    if (heightDiff > GameplayVariables.vars.MaxStepHeight || heightDiff < -GameplayVariables.vars.MaxDropHeight) continue;

                    Vector3Int gridPos = Quantize(newPos, GameplayVariables.vars.SpreadRadius);
                    if (!visitedGrid.Contains(gridPos))
                    {
                        visitedGrid.Add(gridPos);
                        queue.Enqueue((newPos, groundHit.normal, current.depth + 1));
                    }
                }
            }
        }
        return resultNodes;
    }

    private static bool TryFindGround(Vector3 start, out Vector3 hitPoint, out Vector3 normal)
    {
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 5f, 1 << 18))
        {
            hitPoint = hit.point; normal = hit.normal; return true;
        }
        hitPoint = Vector3.zero; normal = Vector3.up; return false;
    }

    private static Vector3Int Quantize(Vector3 position, float cellSize)
    {
        float effectiveCellSize = cellSize * 0.8f;
        return new Vector3Int(
            Mathf.RoundToInt(position.x / effectiveCellSize),
            Mathf.RoundToInt(position.y / 1f),
            Mathf.RoundToInt(position.z / effectiveCellSize)
        );
    }
}