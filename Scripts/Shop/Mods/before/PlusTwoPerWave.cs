using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+2 Fish Per Wave")]
public class PlusTwoPerWave : PlayerModifier
{
    public int add = 2;

    public override void Apply(PlayerController player)
    {
        var mgr = Object.FindObjectOfType<BoidManager>();
        if (!mgr) return;
        mgr.boidsPerWave += add;
    }
}
