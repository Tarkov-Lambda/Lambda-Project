using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Lambda.Core.Networking;
using UnityEngine;

namespace Lambda.Core.Main;

public static class MolotovController
{
    public static float duration = 7f;
    
    // NEW: Maximum absolute distance from the origin the fire is allowed to travel
    public static float maxSpreadDistance = 6.0f; 

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

        // Pass origin directly, TryFindGround will safely calculate the upward offset now
        if (TryFindGround(origin, out Vector3 startPos, out Vector3 startNormal))
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

                // FIX 1: Dynamic Ceiling Check. Prevent raycast from starting inside or above a ceiling.
                float desiredUpOffset = GameplayVariables.vars.MaxStepHeight + 0.1f;
                float safeUpOffset = desiredUpOffset;

                // Cast up from slightly above current position to see if we have headroom
                if (Physics.Raycast(current.pos + Vector3.up * 0.05f, Vector3.up, out RaycastHit ceilHit, desiredUpOffset, 1 << 18))
                {
                    // Clamp the upward step to keep it strictly beneath the low ceiling/prop
                    safeUpOffset = Mathf.Max(0.01f, ceilHit.distance - 0.05f);
                }

                Vector3 rayStart = current.pos + Vector3.up * safeUpOffset;
                Vector3 horizontalOffset = dir * GameplayVariables.vars.SpreadRadius;

                // Horizontal line of sight check
                if (Physics.Raycast(rayStart, dir, out RaycastHit wallHit, GameplayVariables.vars.SpreadRadius, 1 << 18)) 
                    continue;

                Vector3 downRayStart = current.pos + horizontalOffset + Vector3.up * safeUpOffset;
                float maxDownDist = safeUpOffset + GameplayVariables.vars.MaxDropHeight;

                if (Physics.Raycast(downRayStart, Vector3.down, out RaycastHit groundHit, maxDownDist, 1 << 18))
                {
                    Vector3 newPos = groundHit.point;
                    
                    // FIX 2: Distance Cap. Stop the BFS from spreading endlessly in a straight line.
                    if (Vector3.Distance(startPos, newPos) > maxSpreadDistance) 
                        continue;

                    float heightDiff = newPos.y - current.pos.y;

                    if (heightDiff > GameplayVariables.vars.MaxStepHeight || heightDiff < -GameplayVariables.vars.MaxDropHeight) 
                        continue;

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

    private static bool TryFindGround(Vector3 origin, out Vector3 hitPoint, out Vector3 normal)
    {
        float safeUpOffset = 0.5f;

        // Prevent the initial drop-raycast from starting inside geometry if thrown under cars/low overhangs
        if (Physics.Raycast(origin + Vector3.up * 0.05f, Vector3.up, out RaycastHit ceilHit, 0.5f, 1 << 18))
        {
            safeUpOffset = Mathf.Max(0f, ceilHit.distance - 0.05f);
        }

        Vector3 safeStart = origin + Vector3.up * safeUpOffset;

        if (Physics.Raycast(safeStart, Vector3.down, out RaycastHit hit, 5f, 1 << 18))
        {
            hitPoint = hit.point; 
            normal = hit.normal; 
            return true;
        }
        
        hitPoint = Vector3.zero; 
        normal = Vector3.up; 
        return false;
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