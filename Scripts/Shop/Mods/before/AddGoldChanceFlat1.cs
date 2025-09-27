using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/+5% Gold Chance")]
public class AddGoldChanceFlat1 : PlayerModifier
{
    public float add = 0.05f; // 2.5%

    public override void Apply(PlayerController player)
    {
        var mgr = Object.FindObjectOfType<BoidManager>();
        if (!mgr) return;
        mgr.goldenChance = Mathf.Clamp01(mgr.goldenChance + add);
    }
}
