using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Crit Multiplier +1")]
public class CritTripleEffect : PlayerModifier
{
    public override void Apply(PlayerController player)
    {
        // 叠加倍率（基础×2，买一次→×3，再买一次→×4 …）
        GameManager.CritAddMultiplier(1f);
    }
}
