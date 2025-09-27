using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+1 Fish Per Wave")]
public class PlusOnePerWave : PlayerModifier
{
    public int add = 1;

    public override void Apply(PlayerController player)
    {
        var mgr = Object.FindFirstObjectByType<BoidManager>();
        if (!mgr) return;
        mgr.boidsPerWave += add;
    }
}
