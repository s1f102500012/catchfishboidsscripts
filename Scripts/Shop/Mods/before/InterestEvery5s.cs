using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "CatchFish/Modifier/Every 5s gain 2% of current money")]
public class InterestEvery5s : PlayerModifier
{
    [Range(0f, 1f)] public float ratePerItem = 0.02f; // 每件+2%
    public float interval = 5f;

    static int    sStacks = 0;
    static bool   sHooked = false;
    static float  sCarry  = 0f;  // 小数累积，避免低额时取整损失
    static Coroutine sCo;
    static PlayerController sRunner;

    public override void Apply(PlayerController player)
    {
        sStacks++;
        if (!sRunner) sRunner = Object.FindObjectOfType<PlayerController>();
        if (sCo == null && sRunner) sCo = sRunner.StartCoroutine(Loop());

        if (!sHooked)
        {
            GameManager.OnMatchBegin += OnRoundBegin; // 每局开始清零累积
            sHooked = true;
        }
    }

    static void OnRoundBegin() { sCarry = 0f; }

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(5f);
        while (true)
        {
            yield return wait;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsRunning) continue;

            float effRate = Mathf.Max(0f, sStacks) * 0.02f;   // 每件+2%
            if (effRate <= 0f) continue;

            float gainF = gm.CurrentMoney * effRate;
            sCarry += gainF;

            int add = (int)sCarry;                 // 只加整数，余数保留到下次
            if (add > 0)
            {
                gm.AddScore(add);
                sCarry -= add;
            }
        }
    }

    // 供新一大局硬重置
    public static void HardReset()
    {
        sStacks = 0; sCarry = 0f;
        if (sRunner && sCo != null) sRunner.StopCoroutine(sCo);
        sCo = null; sRunner = null;

        if (sHooked)
        {
            GameManager.OnMatchBegin -= OnRoundBegin;
            sHooked = false;
        }
    }
}
