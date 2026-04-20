using System.Collections.Generic;
using UnityEngine;

public interface IRespawnWeights
{
    float SafetyWeight { get; }
    float ObjectiveWeight { get; }
    float TeamCohesionWeight { get; }

    float MaxEnemyDistanceConsidered { get; }
    float IdealObjectiveDistance { get; }
}

public class FFARespawnWeights : IRespawnWeights
{
    public float SafetyWeight => 1.0f;
    public float ObjectiveWeight => 0.0f;
    public float TeamCohesionWeight => 0.0f;
    public float MaxEnemyDistanceConsidered => 100f;
    public float IdealObjectiveDistance => 0f;
}

public class HardpointRespawnWeights : IRespawnWeights
{
    public float SafetyWeight => 0.6f;
    public float ObjectiveWeight => 0.4f;
    public float TeamCohesionWeight => 0.2f;
    public float MaxEnemyDistanceConsidered => 80f;
    public float IdealObjectiveDistance => 35f;
}