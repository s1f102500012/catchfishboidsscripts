using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1003/Spike Hit Accel & Speed Buff")]
public class SpikeHitAccelSpeedBuff : PlayerModifier
{
    public float duration = 5f;
    public float accelMultiplier = 1.5f;
    public float speedMultiplier = 1.25f;

    static bool sEnabled = false;
    static float sDuration = 5f;
    static float sAccelMultiplier = 1.5f;
    static float sSpeedMultiplier = 1.25f;
    static float sBuffEndTime = 0f;

    public override void Apply(PlayerController player)
    {
        sEnabled = true;
        sDuration = Mathf.Max(0f, duration);
        sAccelMultiplier = Mathf.Max(0f, accelMultiplier);
        sSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
    }

    public static void RegisterSpikeHit()
    {
        if (!sEnabled)
            return;
        float end = Time.time + sDuration;
        if (end > sBuffEndTime)
            sBuffEndTime = end;
    }

    public static float CurrentAccelMultiplier
    {
        get
        {
            if (!sEnabled)
                return 1f;
            if (Time.time >= sBuffEndTime)
                return 1f;
            return sAccelMultiplier;
        }
    }

    public static float CurrentSpeedMultiplier
    {
        get
        {
            if (!sEnabled)
                return 1f;
            if (Time.time >= sBuffEndTime)
                return 1f;
            return sSpeedMultiplier;
        }
    }

    public static void HardReset()
    {
        sEnabled = false;
        sDuration = 5f;
        sAccelMultiplier = 1.5f;
        sSpeedMultiplier = 1.25f;
        sBuffEndTime = 0f;
    }
}
