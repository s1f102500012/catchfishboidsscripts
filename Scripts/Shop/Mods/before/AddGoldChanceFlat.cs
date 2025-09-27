using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+2.5% Gold Chance")]
public class AddGoldChanceFlat : PlayerModifier
{
    public float add = 0.025f; // 2.5%

    public override void Apply(PlayerController player)
    {
        var mgr = Object.FindObjectOfType<BoidManager>();
        if (!mgr) return;
        mgr.goldenChance = Mathf.Clamp01(mgr.goldenChance + add);
    }
}
