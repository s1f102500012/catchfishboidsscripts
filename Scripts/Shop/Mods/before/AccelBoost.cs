using UnityEngine;

/// <summary>
/// 加速度提升 × 1.2；可多次叠加。
/// </summary>
[CreateAssetMenu(menuName = "CatchFish/Modifier/AccelBoost")]
public class AccelBoost : PlayerModifier
{
    [Tooltip("乘数，默认 1.2 (= +20%)")]
    public float multiplier = 1.2f;

    public override void Apply(PlayerController player)
    {
        player.accelRate *= multiplier;   // 直接修改公开字段
    }
}
