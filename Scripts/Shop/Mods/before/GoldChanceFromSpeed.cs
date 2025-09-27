using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/0917/Gold chance from speed (per 0.1 over 5 → +1%)")]
public class GoldChanceFromSpeed : PlayerModifier
{
    public float baseline = 5f;         // 基准 5
    public float perStep  = 0.1f;       // 每 0.1
    public float perInc   = 0.01f;      // +1%
    public float pollSec  = 0.2f;       // 轮询间隔

    static int sStacks = 0;
    static bool sHooked = false;
    static PlayerController sPlayer;
    static Coroutine sCo;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (!sPlayer) sPlayer = player ? player : Object.FindFirstObjectByType<PlayerController>();
        if (!sHooked && sPlayer) { sCo = sPlayer.StartCoroutine(Loop()); sHooked = true; }
    }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(0.2f);
        while (true)
        {
            yield return wait;
            var bm = BoidManager.Instance; if (!bm || !sPlayer) continue;

            float spd = sPlayer.maxSpeed;                 // 使用玩家当前最大速度
            float steps = Mathf.Max(0f, (spd - 5f) / 0.1f);
            float add = steps * 0.01f * sStacks;          // 例：7.5→(2.5/0.1)=25→+0.25
            bm.goldenChanceAddFromSpeed = Mathf.Clamp01(add);
        }
    }

    public static void HardReset()
    {
        var bm = BoidManager.Instance;
        if (bm) bm.goldenChanceAddFromSpeed = 0f;

        sStacks = 0;
        if (sHooked && sPlayer && sCo != null) sPlayer.StopCoroutine(sCo);
        sCo = null; sHooked = false; sPlayer = null;
    }
}
