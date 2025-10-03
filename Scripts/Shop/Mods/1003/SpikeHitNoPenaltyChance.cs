using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/1003/Spike Hit No Penalty 50%")]
public class SpikeHitNoPenaltyChance : PlayerModifier
{
    [Range(0f, 1f)] public float noPenaltyChance = 0.5f;

    static bool sEnabled = false;
    static float sChance = 0.5f;

    public override void Apply(PlayerController player)
    {
        sEnabled = true;
        sChance = Mathf.Clamp01(noPenaltyChance);
    }

    public static bool TryPreventPenalty()
    {
        if (!sEnabled)
            return false;
        return Random.value < sChance;
    }

    public static void HardReset()
    {
        sEnabled = false;
        sChance = 0.5f;
    }
}
