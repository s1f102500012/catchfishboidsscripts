// SizeShrink.cs  —— 体型缩小，命中盒同缩
using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/SizeUp")]
public class SizeUp : PlayerModifier
{
    [Range(1.0f, 1.5f)] public float factor = 1.2f;  // 0.8 = 身体缩到 80 %

    public override void Apply(PlayerController p)
    {
        p.transform.localScale *= factor;
        // 若碰撞体随缩放自动调整，可省略；否则请根据自己的 Collider 类型手动缩放半径 / 大小
    }
}
