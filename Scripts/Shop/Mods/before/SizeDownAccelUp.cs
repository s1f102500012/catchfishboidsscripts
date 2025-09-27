using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Size ×0.75 & Accel ×1.25")]
public class SizeDownAccelUp : PlayerModifier
{
    public float sizeMul  = 0.75f;
    public float accelMul = 1.25f;

    public override void Apply(PlayerController player)
    {
        if (!player) return;
        // 等比缩放
        var s = player.transform.localScale;
        player.transform.localScale = new Vector3(s.x * sizeMul, s.y * sizeMul, s.z);
        // 提升加速度
        player.accelRate *= accelMul;
    }
}
