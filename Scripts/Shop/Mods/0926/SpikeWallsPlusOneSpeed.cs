using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0926/Spikes +1 & Player Speed ×1.15")]
public class SpikeWallsPlusOneSpeed : PlayerModifier
{
    const float SpeedMul = 1.15f;
    static int   sSpikeApplied = 0;
    static int   sSpeedStacks  = 0;

    public override void Apply(PlayerController player)
    {
        var sm = Object.FindObjectOfType<SpikeManager>();
        if (sm) { sm.spikesPerRound += 1; sSpikeApplied += 1; }

        var pc = player ? player : Object.FindObjectOfType<PlayerController>();
        if (pc) { pc.maxSpeed *= SpeedMul; sSpeedStacks += 1; }
    }

    public static void HardReset()
    {
        var sm = Object.FindObjectOfType<SpikeManager>();
        if (sm && sSpikeApplied > 0) sm.spikesPerRound = Mathf.Max(1, sm.spikesPerRound - sSpikeApplied);
        sSpikeApplied = 0;

        var pc = Object.FindObjectOfType<PlayerController>();
        if (pc && sSpeedStacks > 0) pc.maxSpeed /= Mathf.Pow(SpeedMul, sSpeedStacks);
        sSpeedStacks = 0;
    }
}
