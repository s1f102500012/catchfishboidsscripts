using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0926/Spikes -1")]
public class SpikeWallsMinusOne : PlayerModifier
{
    static int sApplied = 0;

    public override void Apply(PlayerController player)
    {
        var sm = Object.FindFirstObjectByType<SpikeManager>();
        if (!sm) return;
        if (sm.spikesPerRound > 1) { sm.spikesPerRound -= 1; sApplied += 1; }
    }

    public static void HardReset()
    {
        var sm = Object.FindFirstObjectByType<SpikeManager>();
        if (sm && sApplied > 0) sm.spikesPerRound = Mathf.Max(1, sm.spikesPerRound + sApplied);
        sApplied = 0;
    }
}
