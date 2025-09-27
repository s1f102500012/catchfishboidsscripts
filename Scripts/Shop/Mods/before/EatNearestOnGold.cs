using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Eat +2 nearest smalls on gold")]
public class EatNearestOnGold : PlayerModifier
{
    public int extraPerStack = 2;      // 每件多吞多少条

    static int  sStacks = 0;
    static int  sExtraPerStack = 2;    // 从实例同步到静态
    static bool sHooked = false;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        sExtraPerStack = extraPerStack;
        if (!sHooked) { GlobalEvents.OnGoldFishEaten += OnGold; sHooked = true; }
    }

    static void OnGold(Vector2 _)
    {
        var bm = BoidManager.Instance; if (bm == null) return;
        var pc = Object.FindObjectOfType<PlayerController>(); if (!pc) return;

        int need = sStacks * sExtraPerStack;
        if (need <= 0 || bm.ActiveBoids == null || bm.ActiveBoids.Count == 0) return;

        Vector2 p = pc.transform.position;
        var cand = new List<(Boid b, float d2)>(bm.ActiveBoids.Count);
        foreach (var b in bm.ActiveBoids)
        {
            if (!b || b.isGolden) continue;
            float d2 = ((Vector2)b.transform.position - p).sqrMagnitude;
            cand.Add((b, d2));
        }
        if (cand.Count == 0) return;
        cand.Sort((x, y) => x.d2.CompareTo(y.d2));

        var gm = GameManager.Instance;
        int take = Mathf.Min(need, cand.Count);
        for (int i = 0; i < take; i++)
        {
            var b = cand[i].b; if (!b) continue;

            int times = b.EatCount;
            for (int t = 0; t < times; t++)
            {
                gm.AddScore(b.scoreValue);
                GlobalEvents.RaiseFishEaten(b, b.transform.position);
            }
            if (AudioManager.I) AudioManager.I.PlayEat(false);

            bm.DespawnBoid(b);
        }
    }

    public static void HardReset()
    {
        sStacks = 0; sExtraPerStack = 2;
        if (sHooked){ GlobalEvents.OnGoldFishEaten -= OnGold; sHooked = false; }
    }
}
