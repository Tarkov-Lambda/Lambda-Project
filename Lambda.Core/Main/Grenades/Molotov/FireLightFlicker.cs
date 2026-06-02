using UnityEngine;

public class GroupFireLightFlicker : MonoBehaviour
{
    [Header("Lights")]
    public Light[] lightsToFlicker;

    [Header("Intensity Multiplier")]
    public float minIntensityMultiplier = 0.7f;
    public float maxIntensityMultiplier = 1.3f;
    public float intensitySpeed = 20f;

    [Header("Range Multiplier")]
    public bool affectRange = true;
    public float minRangeMultiplier = 0.9f;
    public float maxRangeMultiplier = 1.1f;
    public float rangeSpeed = 10f;

    [Header("Timing")]
    public float minFlickerInterval = 0.03f;
    public float maxFlickerInterval = 0.12f;

    [Header("Burst")]
    [Range(0f,1f)]
    public float burstChance = 0.15f;
    public float burstStrength = 0.2f;

    private float targetIntensityMult;
    private float targetRangeMult;
    private float currentIntensityMult = 1f;
    private float currentRangeMult = 1f;

    private float timer;

    private float[] baseIntensity;
    private float[] baseRange;

    void Start()
    {
        if (lightsToFlicker.Length == 0)
            return;

        baseIntensity = new float[lightsToFlicker.Length];
        baseRange = new float[lightsToFlicker.Length];

        // Store original values
        for (int i = 0; i < lightsToFlicker.Length; i++)
        {
            if (lightsToFlicker[i] == null)
                continue;

            baseIntensity[i] = lightsToFlicker[i].intensity;
            baseRange[i] = lightsToFlicker[i].range;
        }

        PickNewValues();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PickNewValues();
        }

        currentIntensityMult = Mathf.Lerp(
            currentIntensityMult,
            targetIntensityMult,
            intensitySpeed * Time.deltaTime
        );

        currentRangeMult = Mathf.Lerp(
            currentRangeMult,
            targetRangeMult,
            rangeSpeed * Time.deltaTime
        );

        for (int i = 0; i < lightsToFlicker.Length; i++)
        {
            Light l = lightsToFlicker[i];

            if (l == null)
                continue;

            l.intensity = baseIntensity[i] * currentIntensityMult;

            if (affectRange)
            {
                l.range = baseRange[i] * currentRangeMult;
            }
        }
    }

    void PickNewValues()
    {
        targetIntensityMult = Random.Range(
            minIntensityMultiplier,
            maxIntensityMultiplier
        );

        targetRangeMult = Random.Range(
            minRangeMultiplier,
            maxRangeMultiplier
        );

        // Random flame flare
        if (Random.value < burstChance)
        {
            targetIntensityMult += burstStrength;
        }

        timer = Random.Range(
            minFlickerInterval,
            maxFlickerInterval
        );
    }
}