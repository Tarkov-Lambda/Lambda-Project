
using UnityEngine;

public interface ILambdaObjective
{
    public int NetId { get; }
    public Vector3 Center { get; }
    public Bounds Bounds { get; }
}