using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Spike kills count as player eat")]
public class SpikeKillsCountAsEaten : PlayerModifier
{
    public static bool Enabled { get; private set; }
    public override void Apply(PlayerController player) { Enabled = true; }
    public static void HardReset() { Enabled = false; }

    // 供 Spike 调用
    public static void ResolveAsPlayerEat(Boid b)
    {
        if (!b) return;
        var gm = GameManager.Instance; if (!gm) return;

        int times = b.EatCount;                       // 2^tier
        for (int i = 0; i < times; i++)
        {
            gm.AddScore(b.scoreValue);
            GlobalEvents.RaiseFishEaten(b, b.transform.position); // 统一入口，内部会分发 Gold/Small 事件
        }
        if (AudioManager.I) AudioManager.I.PlayEat(b.isGolden);
    }
}
