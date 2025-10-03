using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1003/Spike Hit Golden Chance Buff")]
public class SpikeHitGoldenChanceBuff : PlayerModifier
{
    public float duration = 5f;
    [Range(0f, 1f)] public float goldenChanceBonus = 0.15f;

    static bool sEnabled = false;
    static float sDuration = 5f;
    static float sGoldenBonus = 0.15f;
    static float sBuffEndTime = 0f;

    public override void Apply(PlayerController player)
    {
        sEnabled = true;
        sDuration = Mathf.Max(0f, duration);
        sGoldenBonus = Mathf.Max(0f, goldenChanceBonus);
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
            return sGoldenBonus;
        }
    }

    public static void HardReset()
    {
        sEnabled = false;
        sDuration = 5f;
        sGoldenBonus = 0.15f;
        sBuffEndTime = 0f;
    }
}
