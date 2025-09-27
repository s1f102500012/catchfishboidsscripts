using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Crit 15% Double Score")]
public class CritDoubleOnScore2 : PlayerModifier
{
    [Range(0f, 1f)] public float addChance = 0.15f;

    public override void Apply(PlayerController player)
    {
        // 仅叠加“固定暴击率”，不再监听事件或加分
        GameManager.CritAddFixedChance(addChance);
        // 倍率默认为 ×2；若有其它道具改为 ×3，由相应道具设置。
    }
}
