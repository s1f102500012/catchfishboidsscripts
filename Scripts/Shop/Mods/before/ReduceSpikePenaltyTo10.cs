using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Spike Penalty 10% (Unique per Run)")]
public class ReduceSpikePenaltyTo10 : PlayerModifier
{
    public override void Apply(PlayerController player)
    {
        var gm = GameManager.Instance;
        if (gm) gm.spikePenaltyRate = 0.05f;   // 需要在 GameManager 里公开该字段，见下文补丁

        // 从本大局的商店池里移除自己（直到新一大局重置）
        var sm = Object.FindFirstObjectByType<ShopManager>();
        if (!sm) return;
        // 在池里找到“指向本 Modifier 的 ItemConfig”并禁用
        foreach (var c in sm.pool)
            if (c && c.modifier == this)
                { ShopManager.BanThisRun(c); break; }
    }
}
