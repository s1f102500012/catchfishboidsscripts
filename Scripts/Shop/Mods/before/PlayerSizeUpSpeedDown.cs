using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Player Size ×1.25, Speed&Accel ×0.95")]
public class PlayerSizeUpSpeedDown : PlayerModifier
{
    public float sizeMult = 1.25f;
    public float moveMult = 0.95f;

    public override void Apply(PlayerController player)
    {
        if (!player) return;

        // 体型放大（等比）
        var s = player.transform.localScale;
        player.transform.localScale = new Vector3(s.x * sizeMult, s.y * sizeMult, s.z);

        // 速度与加速度按比例降低
        player.maxSpeed  *= moveMult;
        player.accelRate *= moveMult;
    }
}
