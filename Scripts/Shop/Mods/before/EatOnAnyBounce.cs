using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/5% count as eaten on ANY bounce")]
public class EatOnAnyBounce : PlayerModifier
{
    [Range(0f,1f)] public float baseChance = 0.05f;

    static int sStacks = 0;
    static float sChance = 0.05f;
    static bool sHooked = false;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sChance = baseChance;
        if (!sHooked){ GlobalEvents.OnFishBoundaryBounce += OnBounce; sHooked = true; }
    }

    static void OnBounce(Boid b, Vector2 pos)
    {
        if (!b) return;
        float eff = 1f - Mathf.Pow(1f - sChance, sStacks);
        if (Random.value >= eff) return;

        var gm = GameManager.Instance; var bm = BoidManager.Instance;
        if (!gm || !bm) return;

        // 结算为“玩家吃掉”（考虑阶数）
        int times = b.EatCount;
        for (int i = 0; i < times; i++)
        {
            gm.AddScore(b.scoreValue);
            GlobalEvents.RaiseFishEaten(b, pos); // 你现有的统一入口
        }
        if (AudioManager.I) AudioManager.I.PlayEat(b.isGolden);

        bm.DespawnBoid(b);
    }

    public static void HardReset()
    {
        sStacks = 0;
        if (sHooked){ GlobalEvents.OnFishBoundaryBounce -= OnBounce; sHooked = false; }
        sChance = 0.05f;
    }
}
