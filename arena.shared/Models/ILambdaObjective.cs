
using UnityEngine;

public interface ILambdaObjective
{
    public string Name { get; }
    public Vector3 Center { get; }
    public Bounds Bounds { get; }
}