using System.Collections.Generic;
using UnityEngine;

public interface IRespawnWeights
{
    float SafetyWeight                  { get; set; }
    float ObjectiveWeight               { get; set; }
    float TeamCohesionWeight            { get; set; }
    float MaxEnemyDistanceConsidered    { get; set; }
    float IdealObjectiveDistance        { get; set; }
}

public class FFARespawnWeights : IRespawnWeights
{
    public float SafetyWeight               { get; set; } = 1.0f;
    public float ObjectiveWeight            { get; set; } = 0.0f;
    public float TeamCohesionWeight         { get; set; } = 0.0f;
    public float MaxEnemyDistanceConsidered { get; set; } = 100f;
    public float IdealObjectiveDistance     { get; set; } = 0f;
}

public class HardpointRespawnWeights : IRespawnWeights
{
    public float SafetyWeight               { get; set; } = 0.6f;
    public float ObjectiveWeight            { get; set; } = 0.4f;
    public float TeamCohesionWeight         { get; set; } = 0.2f;
    public float MaxEnemyDistanceConsidered { get; set; } = 80f;
    public float IdealObjectiveDistance     { get; set; } = 35f;
}