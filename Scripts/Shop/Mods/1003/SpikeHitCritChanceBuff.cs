using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1003/Spike Hit Crit Chance Buff")]
public class SpikeHitCritChanceBuff : PlayerModifier
{
    public float duration = 5f;
    [Range(0f, 1f)] public float critBonus = 0.3f;

    static bool sEnabled = false;
    static float sDuration = 5f;
    static float sCritBonus = 0.3f;
    static float sBuffEndTime = 0f;

    public override void Apply(PlayerController player)
    {
        sEnabled = true;
        sDuration = Mathf.Max(0f, duration);
        sCritBonus = Mathf.Max(0f, critBonus);
    }

    public static void RegisterSpikeHit()
    {
        if (!sEnabled)
            return;
        float end = Time.time + sDuration;
        if (end > sBuffEndTime)
            sBuffEndTime = end;
    }

    public static float CurrentBonus
    {
        get
        {
            if (!sEnabled)
                return 0f;
            if (Time.time >= sBuffEndTime)
                return 0f;
            return sCritBonus;
        }
    }

    public static void HardReset()
    {
        sEnabled = false;
        sDuration = 5f;
        sCritBonus = 0.3f;
        sBuffEndTime = 0f;
    }
}
