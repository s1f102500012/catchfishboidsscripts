using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0923/+1 small per 10% crit")]
public class CritToSmallsPerWave : PlayerModifier
{
    static int sStacks = 0;          // 叠加层数
    static int sApplied = 0;         // 已经施加到 boidsPerWave 的增量
    static Coroutine sCo;
    static PlayerController sRunner;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (!sRunner) sRunner = player ? player : Object.FindObjectOfType<PlayerController>();
        if (sCo == null && sRunner) sCo = sRunner.StartCoroutine(Loop());
        Recalc(); // 立即生效
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(0.33f);
        while (true){ yield return wait; Recalc(); }
    }

    static void Recalc()
    {
        var bm = BoidManager.Instance; var gm = GameManager.Instance;
        if (!bm || !gm) return;

        int target = sStacks * Mathf.FloorToInt(gm.CritChance * 10f); // 每10%加1
        if (target == sApplied) return;

        int delta = target - sApplied;
        bm.boidsPerWave = Mathf.Max(0, bm.boidsPerWave + delta);
        sApplied = target;
    }

    public static void HardReset()
    {
        var bm = BoidManager.Instance;
        if (bm && sApplied != 0) bm.boidsPerWave = Mathf.Max(0, bm.boidsPerWave - sApplied);
        sApplied = 0; sStacks = 0;

        if (sRunner != null && sCo != null) sRunner.StopCoroutine(sCo);
        sCo = null; sRunner = null;
    }
}
